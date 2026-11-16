using UnityEngine;

namespace LiveCollab.UI
{
    /// <summary>Procedurally generated 16x16 icon (no asset files needed).</summary>
    public static class IconFactory
    {
        static Texture2D cachedIcon;

        public static Texture2D Icon
        {
            get
            {
                if (cachedIcon == null) cachedIcon = CreateIcon();
                return cachedIcon;
            }
        }

        static Texture2D CreateIcon()
        {
            const int S = 16;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var colors = new Color32[S * S];
            var center = new Vector2(S * 0.5f, S * 0.5f);
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float sx = x - center.x;
                    float sy = y - center.y;
                    float dist = Mathf.Sqrt(sx * sx + sy * sy) / (S * 0.5f);
                    if (dist <= 0.9f)
                    {
                        float falloff = Mathf.Clamp01(1f - dist);
                        Color col = new Color(0.13f, 0.77f, 0.91f, falloff);
                        float d1 = Vector2.Distance(new Vector2(x, y), new Vector2(S * 0.34f, S * 0.62f));
                        float d2 = Vector2.Distance(new Vector2(x, y), new Vector2(S * 0.66f, S * 0.42f));
                        if (d1 < 2.4f || d2 < 2.4f) col = new Color(1f, 1f, 1f, 1f);
                        colors[y * S + x] = col;
                    }
                    else colors[y * S + x] = new Color32(0, 0, 0, 0);
                }
            }
            tex.SetPixels32(colors);
            tex.Apply();
            return tex;
        }
    }
}
