using UnityEngine;

namespace KerbalWindTunnel.Extensions
{
    public static class DrawingHelper
    {
        /*
         * This method comes from linuxgurugamer's CorrectCOL mod:
         * https://github.com/linuxgurugamer/CorrectCoL/blob/master/source/GraphWindow.cs#L349-L395
         * linuxgurugamer adopted CorrectCOL from Boris-Barboris and so credit goes to both of them.
         * CorrectCOL is released under the MIT license, as such:
         *
         *  Copyright (c) <year> <copyright holders>
         *                [2017] [linuxgurugamer/Boris-Barboris]
         *
         *  Permission is hereby granted, free of charge, to any person obtaining a copy
         *  of this software and associated documentation files (the "Software"), to deal
         *  in the Software without restriction, including without limitation the rights
         *  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
         *  copies of the Software, and to permit persons to whom the Software is
         *  furnished to do so, subject to the following conditions:
         *
         *  The above copyright notice and this permission notice shall be included in all
         *  copies or substantial portions of the Software.
         *
         *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
         *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
         *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
         *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
         *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
         *  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
         *  SOFTWARE.
         *
        */
        public static void DrawLine(ref Texture2D tex, int x1, int y1, int x2, int y2, Color col)
        {
            int width = tex.width, height = tex.height;
            int dy = (y2 - y1);
            int dx = (x2 - x1);
            int stepx, stepy;

            if (dy < 0) { dy = -dy; stepy = -1; }
            else { stepy = 1; }
            if (dx < 0) { dx = -dx; stepx = -1; }
            else { stepx = 1; }
            dy <<= 1;
            dx <<= 1;

            float fraction = 0;

            if (x1 >= 0 && x1 < width && y1 >= 0 && y1 < height)
                tex.SetPixel(x1, y1, col);
            if (dx > dy)
            {
                fraction = dy - (dx >> 1);
                while (Mathf.Abs(x1 - x2) > 1)
                {
                    if (fraction >= 0)
                    {
                        y1 += stepy;
                        fraction -= dx;
                    }
                    x1 += stepx;
                    fraction += dy;
                    if (x1 >= 0 && x1 < width && y1 >= 0 && y1 < height)
                        tex.SetPixel(x1, y1, col);
                }
            }
            else
            {
                fraction = dx - (dy >> 1);
                while (Mathf.Abs(y1 - y2) > 1)
                {
                    if (fraction >= 0)
                    {
                        x1 += stepx;
                        fraction -= dy;
                    }
                    y1 += stepy;
                    fraction += dx;
                    if (x1 >= 0 && x1 < width && y1 >= 0 && y1 < height)
                        tex.SetPixel(x1, y1, col);
                }
            }
        }
    }
}
