using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using KerbalWindTunnel.Extensions;
using Graphing;
using static KerbalWindTunnel.DataGenerators.EnvelopeSurf;
using System.Threading;

namespace KerbalWindTunnel.DataGenerators
{
    public static class EnvelopeLine
    {
        public static void CalculateOptimalLines(Conditions conditions, float exitSpeed, float exitAlt, float initialSpeed, float initialAlt, EnvelopePoint[,] dataArray, CancellationToken cancellationToken, GraphableCollection graphables)
        {
            float[,] accel = dataArray.SelectToArray(pt => pt.Accel_excess * WindTunnelWindow.gAccel);
            float[,] burnRate = dataArray.SelectToArray(pt => pt.fuelBurnRate);
            float timeToClimb(PathSolverCoords current, PathSolverCoords last)
            {
                float dE = Math.Abs(WindTunnelWindow.gAccel * (last.y - current.y) / ((current.x + last.x) / 2) + (last.x - current.x));
                float P = (accel[current.xi, current.yi] + accel[last.xi, last.yi]) / 2;
                return (dE / P);
            }
            float fuelToClimb(PathSolverCoords current, PathSolverCoords last)
            {
                float dF = (burnRate[current.xi, current.yi] + burnRate[last.xi, last.yi]) / 2;
                return timeToClimb(current, last) * dF;
            }

            WindTunnelWindow.Instance.StartCoroutine(ProcessOptimalLine("Fuel-Optimal Path", conditions, exitSpeed, exitAlt, initialSpeed, initialAlt, fuelToClimb, f => f > 0, accel, timeToClimb, cancellationToken, graphables));
            WindTunnelWindow.Instance.StartCoroutine(ProcessOptimalLine("Time-Optimal Path", conditions, exitSpeed, exitAlt, initialSpeed, initialAlt, timeToClimb, f => f > 0, accel, timeToClimb, cancellationToken, graphables));
        }

        private static IEnumerator ProcessOptimalLine(string graphName, Conditions conditions, float exitSpeed, float exitAlt, float initialSpeed, float initialAlt, CostIncreaseFunction costIncreaseFunc, Predicate<float> neighborPredicate, float[,] predicateData, CostIncreaseFunction timeDifferenceFunc, CancellationToken cancellationToken, GraphableCollection graphables)
        {
            Task<List<AscentPathPoint>> task = Task.Factory.StartNew<List<AscentPathPoint>>(
                () =>
                {
                    return GetOptimalPath(conditions, exitSpeed, exitAlt, initialSpeed, initialAlt, costIncreaseFunc, neighborPredicate, predicateData, timeDifferenceFunc, cancellationToken);
                }, cancellationToken
                );

            while (task.Status < TaskStatus.RanToCompletion)
            {
                //Debug.Log(manager.PercentComplete + "% done calculating...");
                yield return 0;
            }

            if (task.Status > TaskStatus.RanToCompletion)
            {
                if (task.Status == TaskStatus.Faulted)
                {
                    Debug.LogError("Wind tunnel task faulted");
                    Debug.LogException(task.Exception);
                }
                else if (task.Status == TaskStatus.Canceled)
                    Debug.Log("Wind tunnel task was canceled.");
                yield break;
            }

            List<AscentPathPoint> results = task.Result;
            if (timeDifferenceFunc != costIncreaseFunc)
                ((MetaLineGraph)graphables[graphName]).SetValues(results.Select(pt => new Vector2(pt.speed, pt.altitude)).ToArray(), new float[][] { results.Select(pt => pt.climbAngle * Mathf.Rad2Deg).ToArray(), results.Select(pt => pt.climbRate).ToArray(), results.Select(pt => pt.cost).ToArray(), results.Select(pt => pt.time).ToArray() });
            else
                ((MetaLineGraph)graphables[graphName]).SetValues(results.Select(pt => new Vector2(pt.speed, pt.altitude)).ToArray(), new float[][] { results.Select(pt => pt.climbAngle * Mathf.Rad2Deg).ToArray(), results.Select(pt => pt.climbRate).ToArray(), results.Select(pt => pt.cost).ToArray() });
            //this.GetOptimalPath(vessel, conditions, 1410, 17700, 0, 0, fuelToClimb, f => f > 0, excessP).Select(pt => new Vector2(pt.speed, pt.altitude)).ToArray());
            //((LineGraph)graphables["Time-Optimal Path"]).SetValues(
            //this.GetOptimalPath(vessel, conditions, 1410, 17700, 0, 0, timeToClimb, f => f > 0, excessP).Select(pt => new Vector2(pt.speed, pt.altitude)).ToArray());
        }

        private delegate float CostIncreaseFunction(PathSolverCoords current, PathSolverCoords last);

        public struct AscentPathPoint
        {
            public readonly float altitude;
            public readonly float speed;
            public readonly float cost;
            public readonly float climbAngle;
            public readonly float climbRate;
            public readonly float time;
            public AscentPathPoint(float speed, float altitude, float cost, float climbRate, float time)
            {
                this.speed = speed;
                this.altitude = altitude;
                this.cost = cost;
                this.climbRate = climbRate;
                if (float.IsNaN(this.climbRate))
                    this.climbRate = 0;
                this.climbAngle = Mathf.Atan2(climbRate, speed);
                this.time = time;
            }
        }
        public struct CoordLocator
        {
            public readonly int x;
            public readonly int y;
            public readonly float value;
            public CoordLocator(int x, int y, float value)
            {
                this.x = x; this.y = y; this.value = value;
            }
            public static CoordLocator[,] GenerateCoordLocators(float[,] values)
            {
                int width = values.GetUpperBound(0);
                int height = values.GetUpperBound(1);
                CoordLocator[,] coordLocators = new CoordLocator[width + 1, height + 1];
                for (int i = 0; i <= width; i++)
                    for (int j = 0; j <= height; j++)
                        coordLocators[i, j] = new CoordLocator(i, j, values[i, j]);
                return coordLocators;
            }
        }

        private static List<AscentPathPoint> GetOptimalPath(Conditions conditions, float exitSpeed, float exitAlt, float initialSpeed, float initialAlt, CostIncreaseFunction costIncreaseFunc, Predicate<float> neighborPredicate, float[,] predicateData, CostIncreaseFunction timeFunc, CancellationToken cancellationToken)
        {
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            System.Diagnostics.Stopwatch profileWatch = new System.Diagnostics.Stopwatch();
            long[] sections = new long[3];

            exitSpeed = Math.Min(exitSpeed, conditions.upperBoundSpeed);
            exitAlt = Math.Min(exitAlt, conditions.upperBoundAltitude);
            initialSpeed = Math.Max(initialSpeed, conditions.lowerBoundSpeed);
            initialAlt = Math.Max(initialAlt, conditions.lowerBoundAltitude);

            stopwatch.Start();

            EnvelopePointExtensions.UniqueQueue<PathSolverCoords> queue = new EnvelopePointExtensions.UniqueQueue<PathSolverCoords>(500);
            PathSolverCoords baseCoord = new PathSolverCoords(conditions.lowerBoundSpeed, conditions.lowerBoundAltitude, conditions.stepSpeed, conditions.stepAltitude, conditions.XResolution, conditions.YResolution);

            float rangeX = (conditions.upperBoundSpeed - conditions.lowerBoundSpeed) - conditions.stepSpeed;
            float rangeY = (conditions.upperBoundAltitude - conditions.lowerBoundAltitude) - conditions.stepAltitude;

            profileWatch.Start();
            float[,] costMatrix = new float[baseCoord.width, baseCoord.height];
            costMatrix.SetAll(float.MaxValue);

            baseCoord = new PathSolverCoords(exitSpeed, exitAlt, baseCoord);
            if (!neighborPredicate(predicateData.Lerp2(exitSpeed / rangeX, exitAlt / rangeY)))
            {
                IEnumerator<CoordLocator> exitCoordFinder = CoordLocator.GenerateCoordLocators(predicateData).GetTaxicabNeighbors(baseCoord.xi, baseCoord.yi, -1, Linq2.Quadrant.II, Linq2.Quadrant.III, Linq2.Quadrant.IV);
                while (exitCoordFinder.MoveNext() && !cancellationToken.IsCancellationRequested && !neighborPredicate(exitCoordFinder.Current.value)) { }
                baseCoord = new PathSolverCoords(exitCoordFinder.Current.x, exitCoordFinder.Current.y, baseCoord);
                exitSpeed = baseCoord.x; exitAlt = baseCoord.y;
            }

            cancellationToken.ThrowIfCancellationRequested();

            costMatrix[baseCoord.xi, baseCoord.yi] = 0;
            foreach (PathSolverCoords c in baseCoord.GetNeighbors(neighborPredicate, predicateData))
                queue.Enqueue(c);

            PathSolverCoords coord;
            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                coord = queue.Dequeue();
                List<PathSolverCoords> neighbors = coord.GetNeighbors(neighborPredicate, predicateData);
                PathSolverCoords bestNeighbor = neighbors[0];
                float bestCost = costMatrix[bestNeighbor.xi, bestNeighbor.yi];
                for(int i = neighbors.Count - 1; i >= 1; i--)
                    if(costMatrix[neighbors[i].xi,neighbors[i].yi] < bestCost)
                    {
                        bestNeighbor = neighbors[i];
                        bestCost = costMatrix[bestNeighbor.xi, bestNeighbor.yi];
                    }
                float newCost = bestCost + costIncreaseFunc(coord, bestNeighbor);
                if(newCost < costMatrix[coord.xi,coord.yi])
                {
                    costMatrix[coord.xi, coord.yi] = newCost;
                    neighbors.Remove(bestNeighbor);
                    for (int i = neighbors.Count - 1; i >= 0; i--)
                        if (costMatrix[neighbors[i].xi, neighbors[i].yi] > newCost)
                            queue.Enqueue(neighbors[i]);
                }
            }
            profileWatch.Stop();
            sections[0] = profileWatch.ElapsedMilliseconds;
            profileWatch.Reset();
            profileWatch.Start();

            float[,] gradientx = new float[baseCoord.width - 1, baseCoord.height - 1];
            float[,] gradienty = new float[baseCoord.width - 1, baseCoord.height - 1];
            for (int i = baseCoord.width - 2; i >= 0; i--)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int j = baseCoord.height - 2; j >= 0; j--)
                {
                    gradientx[i, j] = ((costMatrix[i + 1, j] - costMatrix[i, j]) + (costMatrix[i + 1, j + 1] - costMatrix[i, j + 1])) / 2 / conditions.stepAltitude;
                    gradienty[i, j] = ((costMatrix[i, j + 1] - costMatrix[i, j]) + (costMatrix[i + 1, j + 1] - costMatrix[i + 1, j])) / 2 / conditions.stepSpeed;
                }
            }
            profileWatch.Stop();
            sections[1] = profileWatch.ElapsedMilliseconds;
            profileWatch.Reset();
            profileWatch.Start();

            /*new Graphing.SurfGraph(costMatrix, conditions.lowerBoundSpeed, conditions.upperBoundSpeed, conditions.lowerBoundAltitude, conditions.upperBoundAltitude).
                WriteToFile("costMatrix");
            new Graphing.SurfGraph(gradientx, conditions.lowerBoundSpeed + conditions.stepSpeed / 2, conditions.upperBoundSpeed - conditions.stepSpeed / 2, conditions.lowerBoundAltitude + conditions.stepAltitude / 2, conditions.upperBoundAltitude - conditions.stepAltitude / 2).
                WriteToFile("gradientX");
            new Graphing.SurfGraph(gradienty, conditions.lowerBoundSpeed + conditions.stepSpeed / 2, conditions.upperBoundSpeed - conditions.stepSpeed / 2, conditions.lowerBoundAltitude + conditions.stepAltitude / 2, conditions.upperBoundAltitude - conditions.stepAltitude / 2).
                WriteToFile("gradientY");*/

            List<AscentPathPoint> result = new List<AscentPathPoint>(300);

            int iter = -1;
            coord = new PathSolverCoords(initialSpeed, initialAlt, baseCoord);
            coord = new PathSolverCoords(costMatrix.First(0, 0, f => f >= initialSpeed && f < float.MaxValue / 100) + 1, coord.yi, baseCoord);
            PathSolverCoords lastCoord = coord;
            float lastCost = 0, lastTime = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                iter++;
                if (iter % 10 == 0)
                {
                    //result.Add(new EnvelopePoint(vessel, conditions.body, coord.y, coord.x));
                    if (result.Count > 0)
                    {
                        lastCost = result[result.Count - 1].cost;
                        lastTime = result[result.Count - 1].time;
                    }
                    result.Add(new AscentPathPoint(coord.x, coord.y, costIncreaseFunc(coord, lastCoord) + lastCost, (coord.y - lastCoord.y) / timeFunc(coord, lastCoord), timeFunc(coord, lastCoord) + lastTime));
                    float dx = (coord.x - lastCoord.x) / rangeX;
                    float dy = (coord.y - lastCoord.y) / rangeY;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    //Debug.LogFormat("{0}\t{1}\t{2}", coord.x, coord.y, r);
                    lastCoord = coord;
                    if (r < 0.00001f && iter > 0)
                        break;
                }
                float xW = (coord.x - conditions.stepSpeed / 2) / rangeX;
                float yW = (coord.y - conditions.stepAltitude / 2) / rangeY;
                if (Math.Abs(coord.x - exitSpeed) < 10 && Math.Abs(coord.y - exitAlt) < 100)
                    break;
                try
                {
                    Vector2 d = new Vector2(gradientx.Lerp2(xW, yW), gradienty.Lerp2(xW, yW));
                    if (d.sqrMagnitude <= 0)
                        break;
                    float step = 5 / Mathf.Sqrt(d.x * d.x + (d.y * conditions.stepSpeed / conditions.stepAltitude) * (d.y * conditions.stepSpeed / conditions.stepAltitude));
                    if (coord.y + -d.y * step < 0)
                        coord = coord.Offset(-d.x * step, -coord.y);
                    else
                        coord = coord.Offset(-d * step);
                }
                catch(Exception)
                {
                    Debug.Log("Exception in gradient finding.");
                    Debug.Log(iter);
                    Debug.Log("xW: " + xW + " yW: " + yW);
                    throw;
                }
            }
            coord = new PathSolverCoords(exitSpeed, exitAlt, lastCoord);
            if (result.Count > 0)
            {
                lastCost = result[result.Count - 1].cost;
                lastTime = result[result.Count - 1].time;
            }
            result.Add(new AscentPathPoint(coord.x, coord.y, costIncreaseFunc(coord, lastCoord) + lastCost, (coord.y - lastCoord.y) / timeFunc(coord, lastCoord), timeFunc(coord, lastCoord) + lastTime));
            profileWatch.Stop();
            stopwatch.Stop();

            cancellationToken.ThrowIfCancellationRequested();

            Debug.LogFormat("Time: {0}\tIterations: {1}", stopwatch.ElapsedMilliseconds, iter);
            Debug.LogFormat("Costing: {0}\tGradients: {1}\tMinimizing: {2}", sections[0], sections[1], profileWatch.ElapsedMilliseconds);
            return result;
        }

        private struct PathSolverCoords : IEquatable<PathSolverCoords>
        {
            public readonly int xi;
            public readonly int yi;
            public readonly float x;
            public readonly float y;
            private readonly float stepX;
            private readonly float stepY;
            public readonly float offsetX;
            public readonly float offsetY;
            public readonly int width;
            public readonly int height;

            public List<PathSolverCoords> GetNeighbors()
            {
                List<PathSolverCoords> neighbors = new List<PathSolverCoords>();
                bool[] openings = new bool[] { xi > 0, yi > 0, xi < width - 1, yi < height - 1 };
                if (openings[0])
                    neighbors.Add(new PathSolverCoords(xi - 1, yi, this));
                if (openings[0] && openings[1])
                    neighbors.Add(new PathSolverCoords(xi - 1, yi - 1, this));
                if (openings[1])
                    neighbors.Add(new PathSolverCoords(xi, yi - 1, this));
                if (openings[1] && openings[2])
                    neighbors.Add(new PathSolverCoords(xi + 1, yi - 1, this));
                if (openings[2])
                    neighbors.Add(new PathSolverCoords(xi + 1, yi, this));
                if (openings[2] && openings[3])
                    neighbors.Add(new PathSolverCoords(xi + 1, yi + 1, this));
                if (openings[3])
                    neighbors.Add(new PathSolverCoords(xi, yi + 1, this));
                if (openings[3] && openings[0])
                    neighbors.Add(new PathSolverCoords(xi - 1, yi + 1, this));
                return neighbors;
            }
            public List<PathSolverCoords> GetNeighbors(Predicate<float> predicate, float[,] data)
            {
                List<PathSolverCoords> neighbors = new List<PathSolverCoords>();
                bool[] openings = new bool[] { xi > 0, yi > 0, xi < width - 1, yi < height - 1 };
                if (openings[0] && predicate(data[xi - 1, yi]))
                    neighbors.Add(new PathSolverCoords(xi - 1, yi, this));
                if (openings[0] && openings[1] && predicate(data[xi - 1, yi - 1]))
                    neighbors.Add(new PathSolverCoords(xi - 1, yi - 1, this));
                if (openings[1] && predicate(data[xi, yi - 1]))
                    neighbors.Add(new PathSolverCoords(xi, yi - 1, this));
                if (openings[1] && openings[2] && predicate(data[xi + 1, yi - 1]))
                    neighbors.Add(new PathSolverCoords(xi + 1, yi - 1, this));
                if (openings[2] && predicate(data[xi + 1, yi]))
                    neighbors.Add(new PathSolverCoords(xi + 1, yi, this));
                if (openings[2] && openings[3] && predicate(data[xi + 1, yi + 1]))
                    neighbors.Add(new PathSolverCoords(xi + 1, yi + 1, this));
                if (openings[3] && predicate(data[xi, yi + 1]))
                    neighbors.Add(new PathSolverCoords(xi, yi + 1, this));
                if (openings[3] && openings[0] && predicate(data[xi - 1, yi + 1]))
                    neighbors.Add(new PathSolverCoords(xi - 1, yi + 1, this));
                return neighbors;
            }

            public PathSolverCoords(int xi, int yi, PathSolverCoords similar) :
                this(similar.offsetX, similar.offsetY, similar.stepX, similar.stepY,
                    similar.width, similar.height)
            {
                this.xi = xi;
                this.yi = yi;
                this.x = xi * stepX + offsetX;
                this.y = yi * stepY + offsetY;
            }
            public PathSolverCoords(float x, float y, PathSolverCoords similar) :
                this(similar.offsetX, similar.offsetY, similar.stepX, similar.stepY,
                    similar.width, similar.height)
            {
                this.x = x;
                this.y = y;
                this.xi = Mathf.FloorToInt((x - offsetX) / stepX);
                this.yi = Mathf.FloorToInt((y - offsetY) / stepY);
                if (xi < 0 || xi > width)
                    throw new ArgumentOutOfRangeException("xi", xi, "");
                if (yi < 0 || yi > height)
                    throw new ArgumentOutOfRangeException("yi", yi, "");

            }
            public PathSolverCoords(float offsetX, float offsetY, float stepX, float stepY, int width, int height)
            {
                this.xi = 0;
                this.yi = 0;
                this.x = 0;
                this.y = 0;
                this.offsetX = offsetX;
                this.offsetY = offsetY;
                this.stepX = stepX;
                this.stepY = stepY;
                this.width = width;
                this.height = height;
            }
            public PathSolverCoords Offset(float dx, float dy) => new PathSolverCoords(this.x + dx, this.y + dy, this);
            public PathSolverCoords Offset(Vector2 d) => new PathSolverCoords(this.x + d.x, this.y + d.y, this);
            public override bool Equals(object obj)
            {
                if (obj == null)
                    return false;
                if (obj.GetType() != typeof(PathSolverCoords))
                    return false;
                PathSolverCoords coords = (PathSolverCoords)obj;
                return this.Equals(coords);
            }
            public bool Equals(PathSolverCoords coords)
            {
                return coords.x == this.x && coords.y == this.y;
            }
            public override int GetHashCode()
            {
                return HashCode.Combine(this.x, this.y);
            }
        }
    }

    public static class EnvelopePointExtensions
    {
        public static Vector2[] ToLine(this List<EnvelopePoint> points)
        {
            Vector2[] line = new Vector2[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                line[i] = new Vector2(points[i].speed, points[i].altitude);
            }
            return line;
        }
        public static void SetAll(this float[,] data, float value)
        {
            for (int i = data.GetUpperBound(0); i >= 0; i--)
                for (int j = data.GetUpperBound(1); j >= 0; j--)
                    data[i, j] = value;
        }
        public class UniqueQueue<T> : IEnumerable<T>
        {
            private Queue<T> queue;
            private HashSet<T> hashSet = new HashSet<T>();
            private Dictionary<int, int> collisionCount = new Dictionary<int, int>();

            public UniqueQueue() { queue = new Queue<T>(); }
            public UniqueQueue(int capacity) { queue = new Queue<T>(capacity); }
            public UniqueQueue(IEnumerable<T> collection) { queue = new Queue<T>(collection); hashSet = new HashSet<T>(collection); }

            public float Count
            {
                get => queue.Count;
            }

            public bool Contains(T item)
            {
                return hashSet.Contains(item);
            }

            public T Peek()
            {
                return queue.Peek();
            }

            public bool Enqueue(T item)
            {
                if (!hashSet.Add(item))
                {
                    if (queue.Contains(item))
                        return false;
                    int hash = item.GetHashCode();
                    if (collisionCount.ContainsKey(hash))
                        collisionCount[hash] += 1;
                    else
                        collisionCount[hash] = 1;
                }
                queue.Enqueue(item);
                return true;
            }
            public T Dequeue()
            {
                T item = queue.Dequeue();
                int hash = item.GetHashCode();
                bool collided = collisionCount.TryGetValue(hash, out int count);
                if (collided)
                    count -= 1;
                if (!collided || count <= 0)
                    hashSet.Remove(item);
                return item;
            }

            public IEnumerator<T> GetEnumerator()
            {
                return queue.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return queue.GetEnumerator();
            }
        }
    }
}
