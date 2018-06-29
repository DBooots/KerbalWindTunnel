using UnityEngine;

namespace KerbalWindTunnel.Graphing
{
    public class ColorMap
    {
        public static readonly ColorMap Jet = new ColorMap(ColorMapJet);
        public static readonly ColorMap Jet_Dark = new ColorMap(ColorMapJetDark);

        public delegate Color ColorMapDelegate(float value);
        public delegate bool FilterCriteria(float value);
        ColorMapDelegate colormapDelegate;
        public FilterCriteria Filter { get; set; } = (v) => !float.IsNaN(v) && !float.IsInfinity(v);
        Color FilterColor { get; set; } = Color.clear;
        bool useFunc = false;
        bool Stepped { get; set; } = false;
        Color[] colors;
        int count;

        public ColorMap(params Color[] colors)
        {
            this.colors = colors;
            this.useFunc = false;
            this.count = colors.Length;
        }
        public ColorMap(ColorMapDelegate colorMapFunction)
        {
            this.colormapDelegate = colorMapFunction;
            this.useFunc = true;
        }
        public ColorMap(ColorMap colorMap)
        {
            this.colormapDelegate = colorMap.colormapDelegate;
            this.Filter = colorMap.Filter;
            this.FilterColor = colorMap.FilterColor;
            this.useFunc = colorMap.useFunc;
            this.Stepped = colorMap.Stepped;
            this.colors = colorMap.colors;
            this.count = colorMap.count;
        }

        public Color this[float value]
        {
            get
            {
                if (!Filter(value))
                    return FilterColor;
                if (useFunc)
                    return colormapDelegate(value);
                if (count == 1)
                    return colors[0];
                int index = Mathf.FloorToInt(Mathf.Clamp01(value) * count);
                if (index >= 1)
                    return colors[count - 1];
                if (index <= 0)
                    return colors[0];
                if (Stepped)
                    return colors[index];
                return Color.Lerp(colors[index], colors[index + 1], Mathf.Clamp01(value) * count % 1);
            }
        }

        public static explicit operator Color(ColorMap colorMap)
        {
            return colorMap[0];
        }
        public static implicit operator ColorMap(Color color)
        {
            return new ColorMap(color);
        }

        private static Color ColorMapJet(float value)
        {
            if (float.IsNaN(value))
                return Color.black;

            const float fractional = 1f / 3f;
            const float mins = 128f / 255f;

            if (value < fractional)
            {
                value = (value / fractional * (128 - 255) + 255) / 255;
                return new Color(mins, 1, value, 1);
            }
            if (value < 2 * fractional)
            {
                value = ((value - fractional) / fractional * (255 - 128) + 128) / 255;
                return new Color(value, 1, mins, 1);
            }
            value = ((value - 2 * fractional) / fractional * (128 - 255) + 255) / 255;
            return new Color(1, value, mins, 1);
        }
        private static Color ColorMapJetDark(float value)
        {
            if (float.IsNaN(value))
                return Color.black;

            const float fractional = 0.25f;
            const float mins = 128f / 255f;

            if (value < fractional)
            {
                value = (value / fractional * (255 - 128) + 128) / 255;
                return new Color(mins, value, 1, 1);
            }
            if (value < 2 * fractional)
            {
                value = ((value - fractional) / fractional * (128 - 255) + 255) / 255;
                return new Color(mins, 1, value, 1);
            }
            if (value < 3 * fractional)
            {
                value = ((value - 2 * fractional) / fractional * (255 - 128) + 128) / 255;
                return new Color(value, 1, mins, 1);
            }
            value = ((value - 3 * fractional) / fractional * (128 - 255) + 255) / 255;
            return new Color(1, value, mins, 1);
        }
    }
}
