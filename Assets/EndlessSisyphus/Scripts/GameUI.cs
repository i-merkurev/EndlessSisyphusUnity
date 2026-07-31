using UnityEngine;

namespace EndlessSisyphus
{
    /// <summary>
    /// Экраны и HUD на IMGUI (OnGUI) — без зависимости от uGUI/TMP и без настройки сцены.
    /// Порт HTML-оверлеев: старт «Как преодолевать препятствия», HUD, настройки, Game Over.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        public SisyphusGame game;
        public QuoteDirector quotes;

        const string StartQuote = "«Боги приговорили Сизифа вечно вкатывать на вершину горы камень,\nкоторый, едва достигнув цели, скатывался вниз»";
        const string DefeatQuote = "«Сизиф, бессильный и бунтующий, знает о бесконечности своей печальной участи»";

        Texture2D white, marble, instructionTablet, quoteTablet, authorLogo, buttonNormal, buttonHover, buttonActive;
        Texture2D secondaryButtonNormal, secondaryButtonHover, secondaryButtonActive;
        Font displayFont, uiFont, uiStrongFont, uiBoldFont;
        GUIStyle title, h2, body, hint, btn, banner, quote;
        float UiScale => Mathf.Clamp(
            Mathf.Min(Screen.width / 1280f, Screen.height / 720f),
            0.78f, 1.5f);

        void Awake()
        {
            white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply();
            authorLogo = CreateAuthorLogoTexture();
            marble = CreateMarbleTexture();
            instructionTablet = CreateInstructionTabletTexture();
            quoteTablet = CreateQuoteTabletTexture(instructionTablet);
            buttonNormal = CreateButtonTexture(new Color32(46, 42, 58, 244), new Color32(115, 91, 55, 255), new Color32(85, 73, 66, 255));
            buttonHover = CreateButtonTexture(new Color32(57, 49, 67, 250), new Color32(202, 158, 72, 255), new Color32(121, 96, 68, 255));
            buttonActive = CreateButtonTexture(new Color32(35, 31, 48, 250), new Color32(229, 181, 79, 255), new Color32(76, 62, 57, 255));
            secondaryButtonNormal = CreateButtonTexture(new Color32(35, 32, 46, 218), new Color32(76, 70, 83, 210), new Color32(55, 51, 64, 190), 1);
            secondaryButtonHover = CreateButtonTexture(new Color32(43, 39, 53, 230), new Color32(137, 112, 69, 225), new Color32(80, 68, 60, 205), 1);
            secondaryButtonActive = CreateButtonTexture(new Color32(29, 27, 39, 230), new Color32(161, 127, 67, 230), new Color32(61, 53, 52, 205), 1);
            if (quotes == null) quotes = GetComponent<QuoteDirector>();
        }

        void EnsureStyles()
        {
            if (title != null) return;

            displayFont = Resources.Load<Font>("Fonts/CormorantSC-Bold");
            uiFont = Resources.Load<Font>("Fonts/Jura-Medium");
            uiStrongFont = Resources.Load<Font>("Fonts/Jura-SemiBold");
            uiBoldFont = Resources.Load<Font>("Fonts/Jura-Bold");

            if (displayFont == null || uiFont == null || uiStrongFont == null || uiBoldFont == null)
            {
                Debug.LogError("Не удалось загрузить один или несколько встроенных шрифтов Endless Sisyphus.");
            }
            if (uiFont == null) uiFont = GUI.skin.font;
            if (uiStrongFont == null) uiStrongFont = uiFont;
            if (uiBoldFont == null) uiBoldFont = uiStrongFont;
            if (displayFont == null) displayFont = uiStrongFont;

            title = new GUIStyle(GUI.skin.label) { font = displayFont, fontSize = 34, fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.8f, 0.2f) } };
            h2 = new GUIStyle(GUI.skin.label) { font = uiStrongFont, fontSize = 22, fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.84f, 0.29f, 0.23f) } };
            body = new GUIStyle(GUI.skin.label) { font = uiFont, fontSize = 14, fontStyle = FontStyle.Normal, wordWrap = true, normal = { textColor = new Color(0.96f, 0.91f, 0.82f) } };
            hint = new GUIStyle(GUI.skin.label) { font = uiFont, fontSize = 12, fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.6f, 0.55f, 0.42f) } };
            btn = new GUIStyle(GUI.skin.button)
            {
                font = uiStrongFont,
                fontSize = 16,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(3, 3, 3, 3),
                padding = new RectOffset(12, 12, 6, 6),
                normal = { background = buttonNormal, textColor = new Color(0.82f, 0.76f, 0.63f) },
                hover = { background = buttonHover, textColor = new Color(1f, 0.83f, 0.34f) },
                active = { background = buttonActive, textColor = new Color(1f, 0.76f, 0.24f) },
                focused = { background = buttonHover, textColor = new Color(1f, 0.83f, 0.34f) }
            };
            banner = new GUIStyle(GUI.skin.box) { font = uiFont, fontSize = 14, fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };

            quote = new GUIStyle(GUI.skin.label)
            {
                font = uiFont,
                fontSize = 22,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.19f, 0.20f, 0.20f) }
            };

            LockPassiveStates(title);
            LockPassiveStates(h2);
            LockPassiveStates(body);
            LockPassiveStates(hint);
            LockPassiveStates(banner);
            LockPassiveStates(quote);
        }

        GUIStyle SecondaryButtonStyle(int fontSize)
        {
            var style = new GUIStyle(btn)
            {
                fontSize = fontSize,
                border = new RectOffset(2, 2, 2, 2),
                padding = new RectOffset(8, 8, 3, 3)
            };
            style.normal.background = secondaryButtonNormal;
            style.normal.textColor = new Color(0.61f, 0.59f, 0.57f);
            style.hover.background = secondaryButtonHover;
            style.hover.textColor = new Color(0.86f, 0.76f, 0.52f);
            style.active.background = secondaryButtonActive;
            style.active.textColor = new Color(0.94f, 0.78f, 0.40f);
            style.focused.background = secondaryButtonHover;
            style.focused.textColor = style.hover.textColor;
            return style;
        }

        static void LockPassiveStates(GUIStyle style)
        {
            Color textColor = style.normal.textColor;
            Texture2D background = style.normal.background;
            style.hover.textColor = textColor;
            style.hover.background = background;
            style.active.textColor = textColor;
            style.active.background = background;
            style.focused.textColor = textColor;
            style.focused.background = background;
            style.onNormal.textColor = textColor;
            style.onNormal.background = background;
            style.onHover.textColor = textColor;
            style.onHover.background = background;
            style.onActive.textColor = textColor;
            style.onActive.background = background;
            style.onFocused.textColor = textColor;
            style.onFocused.background = background;
        }

        static GUIStyle PassiveText(GUIStyle source)
        {
            var style = new GUIStyle(source);
            LockPassiveStates(style);
            return style;
        }

        static void PassiveLabel(Rect rect, string text, GUIStyle style) =>
            GUI.Label(rect, text, PassiveText(style));

        static void OutlinedPassiveLabel(Rect rect, string text, GUIStyle style, float stroke, Color strokeColor)
        {
            var outlineStyle = PassiveText(new GUIStyle(style));
            outlineStyle.normal.textColor = strokeColor;
            GUI.Label(new Rect(rect.x - stroke, rect.y, rect.width, rect.height), text, outlineStyle);
            GUI.Label(new Rect(rect.x + stroke, rect.y, rect.width, rect.height), text, outlineStyle);
            GUI.Label(new Rect(rect.x, rect.y - stroke, rect.width, rect.height), text, outlineStyle);
            GUI.Label(new Rect(rect.x, rect.y + stroke, rect.width, rect.height), text, outlineStyle);
            PassiveLabel(rect, text, style);
        }

        void Rect2(Rect r, Color c) { var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = o; }

        static bool IsRightAligned(TextAnchor alignment) =>
            alignment == TextAnchor.UpperRight ||
            alignment == TextAnchor.MiddleRight ||
            alignment == TextAnchor.LowerRight;

        static bool IsCentered(TextAnchor alignment) =>
            alignment == TextAnchor.UpperCenter ||
            alignment == TextAnchor.MiddleCenter ||
            alignment == TextAnchor.LowerCenter;

        static TextAnchor LeftAligned(TextAnchor alignment)
        {
            if (alignment == TextAnchor.MiddleLeft ||
                alignment == TextAnchor.MiddleCenter ||
                alignment == TextAnchor.MiddleRight) return TextAnchor.MiddleLeft;
            if (alignment == TextAnchor.LowerLeft ||
                alignment == TextAnchor.LowerCenter ||
                alignment == TextAnchor.LowerRight) return TextAnchor.LowerLeft;
            return TextAnchor.UpperLeft;
        }

        void TrackedLabel(Rect rect, string text, GUIStyle style, float tracking)
        {
            var glyphStyle = new GUIStyle(style)
            {
                alignment = LeftAligned(style.alignment),
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            LockPassiveStates(glyphStyle);

            float totalWidth = 0f;
            for (int i = 0; i < text.Length; i++)
                totalWidth += glyphStyle.CalcSize(new GUIContent(text[i].ToString())).x;
            totalWidth += Mathf.Max(0, text.Length - 1) * tracking;

            float x = rect.x;
            if (IsCentered(style.alignment)) x += (rect.width - totalWidth) * 0.5f;
            else if (IsRightAligned(style.alignment)) x += rect.width - totalWidth;

            for (int i = 0; i < text.Length; i++)
            {
                string character = text[i].ToString();
                float glyphWidth = glyphStyle.CalcSize(new GUIContent(character)).x;
                GUI.Label(new Rect(x, rect.y, glyphWidth + tracking + 3f, rect.height), character, glyphStyle);
                x += glyphWidth + tracking;
            }
        }

        void TrackedOutlinedLabel(Rect rect, string text, GUIStyle style, float tracking, float stroke, Color strokeColor)
        {
            var outlineStyle = PassiveText(new GUIStyle(style));
            outlineStyle.normal.textColor = strokeColor;
            TrackedLabel(new Rect(rect.x - stroke, rect.y, rect.width, rect.height), text, outlineStyle, tracking);
            TrackedLabel(new Rect(rect.x + stroke, rect.y, rect.width, rect.height), text, outlineStyle, tracking);
            TrackedLabel(new Rect(rect.x, rect.y - stroke, rect.width, rect.height), text, outlineStyle, tracking);
            TrackedLabel(new Rect(rect.x, rect.y + stroke, rect.width, rect.height), text, outlineStyle, tracking);
            TrackedLabel(rect, text, style, tracking);
        }

        float TrackedTextWidth(string text, GUIStyle style, float tracking)
        {
            float width = 0f;
            for (int i = 0; i < text.Length; i++)
                width += style.CalcSize(new GUIContent(text[i].ToString())).x;
            return width + Mathf.Max(0, text.Length - 1) * tracking;
        }

        void DrawNarrativeLine(Rect rect, GUIStyle regularStyle, GUIStyle keyStyle, float tracking,
            float keyWeight, string firstKey, string middle, string secondKey = null, string suffix = null)
        {
            var regular = PassiveText(new GUIStyle(regularStyle) { alignment = TextAnchor.MiddleLeft, wordWrap = false });
            var key = PassiveText(new GUIStyle(keyStyle) { alignment = TextAnchor.MiddleLeft, wordWrap = false });
            float firstWidth = TrackedTextWidth(firstKey, key, tracking);
            float middleWidth = regular.CalcSize(new GUIContent(middle)).x;
            float secondWidth = string.IsNullOrEmpty(secondKey) ? 0f : TrackedTextWidth(secondKey, key, tracking);
            float suffixWidth = string.IsNullOrEmpty(suffix) ? 0f : regular.CalcSize(new GUIContent(suffix)).x;
            float x = rect.x + (rect.width - firstWidth - middleWidth - secondWidth - suffixWidth) * 0.5f;

            TrackedOutlinedLabel(new Rect(x, rect.y, firstWidth, rect.height), firstKey, key,
                tracking, keyWeight, key.normal.textColor);
            x += firstWidth;
            PassiveLabel(new Rect(x, rect.y, middleWidth, rect.height), middle, regular);
            x += middleWidth;
            if (!string.IsNullOrEmpty(secondKey))
            {
                TrackedOutlinedLabel(new Rect(x, rect.y, secondWidth, rect.height), secondKey, key,
                    tracking, keyWeight, key.normal.textColor);
                x += secondWidth;
            }
            if (!string.IsNullOrEmpty(suffix))
                PassiveLabel(new Rect(x, rect.y, suffixWidth, rect.height), suffix, regular);
        }

        void DrawMetric(Rect rect, string label, string value, GUIStyle labelStyle, GUIStyle valueStyle,
            float labelTracking, float valueTracking, float gap, bool alignRight)
        {
            var left = PassiveText(new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleLeft });
            var right = PassiveText(new GUIStyle(valueStyle) { alignment = TextAnchor.MiddleLeft });
            float labelWidth = TrackedTextWidth(label, left, labelTracking);
            float valueWidth = TrackedTextWidth(value, right, valueTracking);
            float total = labelWidth + gap + valueWidth;
            float x = alignRight ? rect.xMax - total : rect.x + (rect.width - total) * 0.5f;
            TrackedLabel(new Rect(x, rect.y, labelWidth, rect.height), label, left, labelTracking);
            TrackedLabel(new Rect(x + labelWidth + gap, rect.y, valueWidth, rect.height), value, right, valueTracking);
        }

        void DrawTitleDivider(float centerX, float y, float width, float scale)
        {
            Color gold = new Color(0.91f, 0.81f, 0.48f, 0.82f);
            float gap = 8f * scale;
            float half = width * 0.5f;
            Rect2(new Rect(centerX - half, y, half - gap, 1f), gold);
            Rect2(new Rect(centerX + gap, y, half - gap, 1f), gold);

            var oldMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, new Vector2(centerX, y + 0.5f));
            Rect2(new Rect(centerX - 2.5f * scale, y - 2f * scale, 5f * scale, 5f * scale), gold);
            GUI.matrix = oldMatrix;
        }

        Texture2D CreateButtonTexture(Color32 fill, Color32 border, Color32 highlight, int edgeSize = 2)
        {
            const int size = 12;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool edge = x < edgeSize || x >= size - edgeSize || y < edgeSize || y >= size - edgeSize;
                    bool topEdge = y >= size - edgeSize - 1 && x >= edgeSize && x < size - edgeSize;
                    pixels[y * size + x] = edge ? border : topEdge ? highlight : fill;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;
            return texture;
        }

        Texture2D CreateAuthorLogoTexture()
        {
            const int size = 48;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            Color32 stone = new Color32(194, 179, 135, 255);
            Color32 stoneShadow = new Color32(135, 123, 105, 255);
            Color32 curls = new Color32(74, 66, 70, 255);
            Color32 curlLight = new Color32(112, 98, 91, 255);

            void Plot(int x, int y, Color32 color)
            {
                if (x >= 0 && x < size && y >= 0 && y < size)
                    pixels[y * size + x] = color;
            }

            void Block(int x, int y, int width, int height, Color32 color)
            {
                for (int yy = y; yy < y + height; yy++)
                    for (int xx = x; xx < x + width; xx++)
                        Plot(xx, yy, color);
            }

            void Disc(int cx, int cy, int radius, Color32 color)
            {
                int rr = radius * radius;
                for (int y = cy - radius; y <= cy + radius; y++)
                    for (int x = cx - radius; x <= cx + radius; x++)
                    {
                        int dx = x - cx;
                        int dy = y - cy;
                        if (dx * dx + dy * dy <= rr) Plot(x, y, color);
                    }
            }

            // Симметричная каменная голова анфас.
            Block(13, 2, 25, 5, stoneShadow);
            Block(20, 6, 12, 10, stone);
            for (int y = 12; y <= 38; y++)
            {
                float dy = (y - 25f) / 14f;
                for (int x = 12; x <= 36; x++)
                {
                    float dx = (x - 24f) / 11f;
                    if (dx * dx + dy * dy <= 1f) Plot(x, y, stone);
                }
            }
            Disc(12, 25, 3, stoneShadow);
            Disc(36, 25, 3, stoneShadow);
            Plot(19, 28, new Color32(45, 42, 48, 255));
            Plot(29, 28, new Color32(45, 42, 48, 255));
            Block(23, 21, 3, 7, stoneShadow); // прямой нос
            Block(21, 18, 7, 1, stoneShadow); // спокойная линия губ
            Block(19, 13, 10, 2, stone);      // чистый подбородок без бороды

            // Кудри симметрично обрамляют верх головы, не заходя на подбородок.
            int[,] curlCenters =
            {
                {13, 31}, {15, 37}, {20, 41}, {24, 42},
                {28, 41}, {33, 37}, {35, 31}, {18, 35}, {30, 35}
            };
            for (int i = 0; i < curlCenters.GetLength(0); i++)
            {
                int cx = curlCenters[i, 0];
                int cy = curlCenters[i, 1];
                Disc(cx, cy, 4, curls);
                Disc(cx - 1, cy + 1, 1, curlLight);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;
            return texture;
        }

        Texture2D CreateMarbleTexture()
        {
            const int w = 128, h = 64;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * h];
            var random = new System.Random(1975);
            float ox = (float)random.NextDouble() * 20f;
            float oy = (float)random.NextDouble() * 20f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float broad = Mathf.PerlinNoise(ox + x * 0.035f, oy + y * 0.08f);
                    float grain = Mathf.PerlinNoise(ox + x * 0.21f, oy + y * 0.25f);
                    float veinWave = Mathf.Sin(x * 0.055f + y * 0.22f + broad * 5f);
                    float vein = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(veinWave)), 9f);
                    float value = 0.27f + (broad - 0.5f) * 0.065f + (grain - 0.5f) * 0.020f - vein * 0.026f;
                    pixels[y * w + x] = new Color(value * 1.02f, value * 0.96f, value, 1f);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Point;
            return texture;
        }

        Texture2D CreateInstructionTabletTexture()
        {
            const int width = 512;
            const int height = 256;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            var random = new System.Random(2027);
            float ox = (float)random.NextDouble() * 18f;
            float oy = (float)random.NextDouble() * 18f;
            var outline = new[]
            {
                new Vector2(20f, 8f), new Vector2(488f, 8f),
                new Vector2(488f, 12f), new Vector2(496f, 12f),
                new Vector2(496f, 20f), new Vector2(504f, 20f),
                new Vector2(504f, 232f), new Vector2(500f, 232f),
                new Vector2(500f, 240f), new Vector2(492f, 240f),
                new Vector2(492f, 248f), new Vector2(20f, 248f),
                new Vector2(20f, 244f), new Vector2(12f, 244f),
                new Vector2(12f, 236f), new Vector2(8f, 236f),
                new Vector2(8f, 20f), new Vector2(12f, 20f),
                new Vector2(12f, 12f), new Vector2(20f, 12f)
            };

            Color marbleTop = new Color32(146, 150, 166, 255);
            Color marbleBottom = new Color32(109, 115, 133, 255);
            Color nightOverlay = new Color32(44, 42, 61, 255);
            Color veinColor = new Color32(86, 91, 110, 255);
            Color crackColor = new Color32(74, 77, 92, 255);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var point = new Vector2(x + 0.5f, y + 0.5f);
                    if (!PointInPolygon(point, outline))
                    {
                        pixels[y * width + x] = Color.clear;
                        continue;
                    }
                    float silhouetteDistance = PolygonEdgeDistance(point, outline);

                    float vertical = y / (height - 1f);
                    Color stone = Color.Lerp(marbleTop, marbleBottom, vertical);
                    float broad = Mathf.PerlinNoise(ox + x * 0.021f, oy + y * 0.034f) - 0.5f;
                    float grain = Mathf.PerlinNoise(ox + x * 0.15f, oy + y * 0.17f) - 0.5f;
                    float naturalVariation = broad * 0.035f + grain * 0.012f;
                    stone.r = Mathf.Clamp01(stone.r + naturalVariation);
                    stone.g = Mathf.Clamp01(stone.g + naturalVariation);
                    stone.b = Mathf.Clamp01(stone.b + naturalVariation * 1.1f);
                    float nightAmount = Mathf.Lerp(0.12f, 0.28f,
                        Mathf.Clamp01(vertical * 0.65f + Mathf.PerlinNoise(ox + x * 0.009f, oy + y * 0.012f) * 0.35f));
                    Color nightMultiplied = new Color(
                        stone.r * nightOverlay.r,
                        stone.g * nightOverlay.g,
                        stone.b * nightOverlay.b,
                        stone.a);
                    stone = Color.Lerp(stone, nightMultiplied, nightAmount);

                    float veinDistance = TabletVeinDistance(x, y);
                    if (veinDistance < 1.65f)
                        stone = Color.Lerp(stone, veinColor, (1f - veinDistance / 1.65f) * 0.25f);

                    float crackDistance = TabletCrackDistance(x, y);
                    if (crackDistance < 1.25f)
                        stone = Color.Lerp(stone, crackColor, (1f - crackDistance / 1.25f) * 0.68f);

                    stone.a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(silhouetteDistance / 1.35f));
                    pixels[y * width + x] = stone;
                }
            }

            pixels = PixelateTabletPixels(pixels, width, height, 4);
            texture.SetPixels(pixels);
            texture.Apply(false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;
            return texture;
        }

        static Color[] PixelateTabletPixels(Color[] source, int width, int height, int blockSize)
        {
            var result = new Color[source.Length];
            for (int blockY = 0; blockY < height; blockY += blockSize)
            {
                for (int blockX = 0; blockX < width; blockX += blockSize)
                {
                    Color average = Color.clear;
                    float alphaSum = 0f;
                    int samples = 0;
                    int maxY = Mathf.Min(blockY + blockSize, height);
                    int maxX = Mathf.Min(blockX + blockSize, width);

                    for (int y = blockY; y < maxY; y++)
                    {
                        for (int x = blockX; x < maxX; x++)
                        {
                            Color sample = source[y * width + x];
                            alphaSum += sample.a;
                            samples++;
                            if (sample.a <= 0.01f) continue;
                            average.r += sample.r * sample.a;
                            average.g += sample.g * sample.a;
                            average.b += sample.b * sample.a;
                            average.a += sample.a;
                        }
                    }

                    float coverage = samples > 0 ? alphaSum / samples : 0f;
                    Color blockColor = Color.clear;
                    if (coverage >= 0.46f && average.a > 0.001f)
                    {
                        blockColor = new Color(
                            average.r / average.a,
                            average.g / average.a,
                            average.b / average.a,
                            1f);
                    }

                    for (int y = blockY; y < maxY; y++)
                        for (int x = blockX; x < maxX; x++)
                            result[y * width + x] = blockColor;
                }
            }
            return result;
        }

        Texture2D CreateQuoteTabletTexture(Texture2D source)
        {
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            Color[] pixels = source.GetPixels();
            Color lift = new Color(0.78f, 0.80f, 0.87f, 1f);
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a <= 0.001f) continue;
                float alpha = pixels[i].a;
                pixels[i] = Color.Lerp(pixels[i], lift, 0.22f);
                pixels[i].a = alpha;
            }
            texture.SetPixels(pixels);
            texture.Apply(false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;
            return texture;
        }

        static bool PointInPolygon(Vector2 point, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];
                if ((a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        static float PolygonEdgeDistance(Vector2 point, Vector2[] polygon)
        {
            float distance = float.MaxValue;
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % polygon.Length];
                distance = Mathf.Min(distance, SegmentDistance(point.x, point.y, a.x, a.y, b.x, b.y));
            }
            return distance;
        }

        static float TabletVeinDistance(float x, float y)
        {
            float d = float.MaxValue;
            d = Mathf.Min(d, SegmentDistance(x, y, 91f, 12f, 112f, 34f));
            d = Mathf.Min(d, SegmentDistance(x, y, 112f, 34f, 148f, 48f));
            d = Mathf.Min(d, SegmentDistance(x, y, 112f, 34f, 129f, 63f));
            d = Mathf.Min(d, SegmentDistance(x, y, 322f, 246f, 317f, 222f));
            d = Mathf.Min(d, SegmentDistance(x, y, 317f, 222f, 346f, 201f));
            d = Mathf.Min(d, SegmentDistance(x, y, 317f, 222f, 290f, 207f));
            d = Mathf.Min(d, SegmentDistance(x, y, 497f, 86f, 473f, 93f));
            d = Mathf.Min(d, SegmentDistance(x, y, 473f, 93f, 450f, 113f));
            return d;
        }

        static float TabletCrackDistance(float x, float y)
        {
            float d = float.MaxValue;
            d = Mathf.Min(d, SegmentDistance(x, y, 482f, 26f, 458f, 49f));
            d = Mathf.Min(d, SegmentDistance(x, y, 458f, 49f, 430f, 61f));
            d = Mathf.Min(d, SegmentDistance(x, y, 458f, 49f, 468f, 77f));
            d = Mathf.Min(d, SegmentDistance(x, y, 29f, 220f, 55f, 202f));
            d = Mathf.Min(d, SegmentDistance(x, y, 55f, 202f, 83f, 190f));
            d = Mathf.Min(d, SegmentDistance(x, y, 55f, 202f, 65f, 226f));
            d = Mathf.Min(d, SegmentDistance(x, y, 490f, 151f, 463f, 149f));
            d = Mathf.Min(d, SegmentDistance(x, y, 463f, 149f, 439f, 137f));
            d = Mathf.Min(d, SegmentDistance(x, y, 463f, 149f, 447f, 169f));
            return d;
        }

        static float SegmentDistance(float px, float py, float ax, float ay, float bx, float by)
        {
            float abx = bx - ax;
            float aby = by - ay;
            float lengthSquared = abx * abx + aby * aby;
            float t = lengthSquared > 0f ? Mathf.Clamp01(((px - ax) * abx + (py - ay) * aby) / lengthSquared) : 0f;
            float dx = px - (ax + abx * t);
            float dy = py - (ay + aby * t);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        void Frame(Rect r, float thickness, Color color)
        {
            Rect2(new Rect(r.x, r.y, r.width, thickness), color);
            Rect2(new Rect(r.x, r.yMax - thickness, r.width, thickness), color);
            Rect2(new Rect(r.x, r.y, thickness, r.height), color);
            Rect2(new Rect(r.xMax - thickness, r.y, thickness, r.height), color);
        }

        void DrawBevel(Rect r, float thickness, Color light, Color dark)
        {
            Rect2(new Rect(r.x, r.y, r.width, thickness), light);
            Rect2(new Rect(r.x, r.y, thickness, r.height), light);
            Rect2(new Rect(r.x, r.yMax - thickness, r.width, thickness), dark);
            Rect2(new Rect(r.xMax - thickness, r.y, thickness, r.height), dark);
        }

        void DrawStonePanel(Rect r, float alpha)
        {
            float scale = UiScale;
            float panelAlpha = alpha * 0.98f;
            var old = GUI.color;
            GUI.color = new Color(0.08f, 0.07f, 0.12f, 0.42f * alpha);
            GUI.DrawTexture(new Rect(r.x + 5f * scale, r.y + 7f * scale, r.width, r.height),
                quoteTablet, ScaleMode.StretchToFill, true);
            GUI.color = new Color(1f, 1f, 1f, panelAlpha);
            GUI.DrawTexture(r, quoteTablet, ScaleMode.StretchToFill, true);
            GUI.color = old;
        }

        void DrawInstructionTablet(Rect r, float scale)
        {
            var old = GUI.color;
            GUI.color = new Color(0.025f, 0.022f, 0.035f, 0.40f);
            GUI.DrawTexture(new Rect(r.x + 7f * scale, r.y + 10f * scale, r.width, r.height), instructionTablet, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;
            GUI.DrawTexture(r, instructionTablet, ScaleMode.StretchToFill, true);
            GUI.color = old;
        }

        Rect QuoteRect(string text, float alpha)
        {
            float scale = UiScale;
            float width = Mathf.Min(Screen.width * 0.74f, 900f * scale);
            int baseSize = text.Length > 170 ? 20 : text.Length > 105 ? 22 : 24;
            quote.fontSize = Mathf.RoundToInt(baseSize * scale);
            float textHeight = quote.CalcHeight(new GUIContent(text), width - 52f * scale);
            float height = Mathf.Clamp(textHeight + 42f * scale, 84f * scale, Mathf.Min(172f * scale, Screen.height * 0.23f));
            float slide = (1f - alpha) * 18f * scale;
            return new Rect((Screen.width - width) * 0.5f, Screen.height - height - 22f * scale + slide, width, height);
        }

        Rect DrawStoneQuote(string text, float alpha)
        {
            Rect r = QuoteRect(text, alpha);
            return DrawStoneQuoteAt(r, text, alpha);
        }

        Rect DrawStoneQuoteAt(Rect r, string text, float alpha)
        {
            DrawStonePanel(r, alpha);

            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            float scale = UiScale;
            Rect textRect = new Rect(r.x + 26f * scale, r.y + 16f * scale, r.width - 52f * scale, r.height - 32f * scale);
            quote.normal.textColor = new Color(0.012f, 0.010f, 0.015f);
            PassiveLabel(textRect, text, quote);
            GUI.color = old;
            return r;
        }

        void OnGUI()
        {
            if (game == null) return;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            if (quotes == null) quotes = GetComponent<QuoteDirector>();
            EnsureStyles();
            switch (game.State)
            {
                case GState.Start: DrawStart(); break;
                case GState.Settings: DrawSettings(); break;
                case GState.Playing:
                    DrawHUD(); break;
                case GState.Falling:
                    break;
                case GState.Over: DrawOver(); break;
            }
        }

        void DrawHUD()
        {
            float w = Screen.width, h = Screen.height;
            float scale = UiScale;
            bool showQuote = quotes != null && quotes.IsVisible;
            Rect quoteArea = showQuote ? QuoteRect(quotes.CurrentText, quotes.Opacity) : new Rect();
            // шкала сил
            float bx = 26f * scale, by = 46f * scale, bw = Mathf.Min(w * 0.34f, 400f * scale), bh = 14f * scale;
            var staminaLabel = new GUIStyle(hint)
            {
                font = uiBoldFont,
                fontStyle = FontStyle.Normal,
                fontSize = Mathf.RoundToInt(19f * scale),
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.79f, 0.76f, 0.69f) }
            };
            TrackedLabel(new Rect(bx, by - 33f * scale, bw, 28f * scale), "СИЛЫ", staminaLabel, 2.1f * scale);
            Rect barRect = new Rect(bx, by, bw, bh);
            Rect2(barRect, new Color(0.035f, 0.03f, 0.07f, 0.58f));
            float pct = game.Stamina / GameConfig.StaminaMax;
            Color fill = pct > 0.5f
                ? new Color(0.68f, 0.57f, 0.31f)
                : pct > 0.22f ? new Color(0.88f, 0.66f, 0.27f) : new Color(0.72f, 0.31f, 0.25f);
            Rect2(new Rect(bx, by, bw * pct, bh), fill);

            // высота / рекорд
            var heightLabel = new GUIStyle(body) { font = displayFont, fontStyle = FontStyle.Normal, fontSize = Mathf.RoundToInt(30f * scale) };
            var heightValue = new GUIStyle(body) { font = displayFont, fontStyle = FontStyle.Normal, fontSize = Mathf.RoundToInt(30f * scale) };
            DrawMetric(new Rect(w - 430f * scale, 12f * scale, 410f * scale, 44f * scale),
                "ВЫСОТА", Roman.To(game.Height), heightLabel, heightValue, 2f * scale, 1.5f * scale, 12f * scale, true);
            var right2 = new GUIStyle(hint) { font = uiFont, fontSize = Mathf.RoundToInt(18f * scale), alignment = TextAnchor.UpperRight, normal = { textColor = new Color(0.72f, 0.69f, 0.62f) } };
            TrackedLabel(new Rect(w - 430f * scale, 62f * scale, 410f * scale, 30f * scale), "РЕКОРД  " + Roman.To(game.Best), right2, 1.5f * scale);

            // баннер препятствия
            var hudBanner = new GUIStyle(hint)
            {
                font = uiBoldFont,
                fontStyle = FontStyle.Normal,
                fontSize = Mathf.RoundToInt(20f * scale),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.95f, 0.82f) }
            };
            string bt = BannerText();
            if (bt != null)
                OutlinedPassiveLabel(new Rect(w / 2f - 230f * scale, 88f * scale, 460f * scale, 42f * scale),
                    bt, hudBanner, Mathf.Max(0.45f, 0.35f * scale), new Color(0.035f, 0.03f, 0.07f, 0.96f));

            // индикатор осторожного режима
            if (game.Careful)
            {
                bool exitGrace = game.RainExitGrace > 0f;
                var cs = new GUIStyle(hudBanner)
                {
                    normal =
                    {
                        textColor = game.CarefulBad
                            ? new Color(0.84f, 0.29f, 0.23f)
                            : exitGrace ? new Color(0.91f, 0.81f, 0.48f) : new Color(0.49f, 0.94f, 0.82f)
                    }
                };
                string ct = game.CarefulBad
                    ? "ОТКЛЮЧИ C — НЕТ ДОЖДЯ"
                    : exitGrace ? "ДОЖДЬ ПРОШЁЛ — ОТКЛЮЧИ C" : "ОСТОРОЖНО (C)";
                float carefulY = showQuote ? quoteArea.y - 38f * scale : h - 60f * scale;
                PassiveLabel(new Rect(w / 2f - 200f * scale, carefulY, 400f * scale, 30f * scale), ct, cs);
            }

            if (showQuote) DrawStoneQuote(quotes.CurrentText, quotes.Opacity);
        }

        string BannerText()
        {
            if (game.WindExitGrace > 0f)
                return "ВЕТЕР СТИХ — ТОЛКАЙ SPACE";

            switch (game.Obstacle)
            {
                case ObKind.Wind:
                    return (game.WindVariant == 0 ? "ВЕТЕР" : game.WindVariant == 1 ? "ПОРЫВЫ" : "ТУРБУЛЕНТНЫЙ ВЕТЕР") +
                        " — НЕ ЖМИ КЛАВИШИ";
                case ObKind.Rain:
                    return (game.RainVariant == 0 ? "МОРОСЬ" : game.RainVariant == 1 ? "ДОЖДЬ" : "ЛИВЕНЬ") +
                        " — C + SPACE";
                case ObKind.Ice: return game.ActiveIceDistance <= game.VW * 0.28f ? "ЛЁД — ДЕРЖИ SPACE" : null;
                case ObKind.Steep: return game.ActiveSteepDistance <= game.VW * 0.30f ? "КРУТОЙ СКЛОН — SHIFT + SPACE" : null;
                default: return null;
            }
        }

        Rect Center(float cw, float ch) => new Rect(Screen.width / 2 - cw / 2, Screen.height / 2 - ch / 2, cw, ch);

        void DrawStart()
        {
            Rect2(new Rect(0, 0, Screen.width, Screen.height), new Color(0.04f, 0.03f, 0.07f, 0.82f));
            float scale = UiScale;
            float contentW = Mathf.Min(Screen.width * 0.72f, 900f * scale);
            float x = (Screen.width - contentW) * 0.5f;
            float top = Mathf.Max(14f, Screen.height * 0.035f);

            var startTitle = new GUIStyle(title) { fontSize = Mathf.Max(34, Mathf.RoundToInt(44f * scale)) };
            var epigraph = PassiveText(new GUIStyle(quote)
            {
                font = uiFont,
                fontSize = Mathf.Max(14, Mathf.RoundToInt(18f * scale)),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.68f, 0.64f, 0.58f, 0.88f) }
            });
            var tabletTitle = PassiveText(new GUIStyle(body)
            {
                font = uiBoldFont,
                fontSize = Mathf.Max(17, Mathf.RoundToInt(19f * scale)),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color32(6, 7, 10, 255) }
            });
            var tabletIntro = PassiveText(new GUIStyle(body)
            {
                font = uiFont,
                fontSize = Mathf.Max(14, Mathf.RoundToInt(17f * scale)),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color32(6, 7, 10, 255) }
            });
            var narrative = PassiveText(new GUIStyle(body)
            {
                font = uiFont,
                fontSize = Mathf.Max(13, Mathf.RoundToInt(17f * scale)),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                richText = false,
                normal = { textColor = new Color32(6, 7, 10, 255) }
            });
            var narrativeKey = PassiveText(new GUIStyle(narrative)
            {
                font = uiBoldFont,
                fontSize = narrative.fontSize + Mathf.Max(1, Mathf.RoundToInt(0.7f * scale)),
                fontStyle = FontStyle.Normal,
                normal = { textColor = new Color32(0, 0, 2, 255) }
            });
            var tabletFinal = PassiveText(new GUIStyle(narrative)
            {
                font = uiFont,
                fontSize = Mathf.Max(14, Mathf.RoundToInt(17f * scale)),
                normal = { textColor = new Color32(6, 7, 10, 255) }
            });
            TrackedLabel(new Rect(x, top, contentW, 60f * scale), "БЕСКОНЕЧНЫЙ СИЗИФ", startTitle, 4.5f * scale);
            DrawTitleDivider(Screen.width * 0.5f, top + 64f * scale, Mathf.Min(170f * scale, contentW * 0.3f), scale);
            PassiveLabel(new Rect(x + 40f * scale, top + 80f * scale, contentW - 80f * scale, 62f * scale), StartQuote, epigraph);

            Rect tablet = new Rect(x, top + 150f * scale, contentW, 348f * scale);
            DrawInstructionTablet(tablet, scale);
            float accentWeight = 0.18f * scale;
            TrackedOutlinedLabel(new Rect(tablet.x + 36f * scale, tablet.y + 20f * scale, tablet.width - 72f * scale, 36f * scale),
                "КАК ПРЕОДОЛЕВАТЬ ПРЕПЯТСТВИЯ", tabletTitle, 1.5f * scale,
                accentWeight, tabletTitle.normal.textColor);
            PassiveLabel(new Rect(tablet.x + 46f * scale, tablet.y + 62f * scale, tablet.width - 92f * scale, 42f * scale),
                "Толкай камень вверх по бесконечному склону — так высоко, как хватит сил.", tabletIntro);

            float storyY = tablet.y + 112f * scale;
            float storyGap = 35f * scale;
            Rect storyRect = new Rect(tablet.x + 48f * scale, storyY, tablet.width - 96f * scale, 31f * scale);
            float keyTracking = 0.35f * scale;
            DrawNarrativeLine(storyRect, narrative, narrativeKey, keyTracking, accentWeight,
                "ЛЁД", " — держи ", "SPACE", ", чтобы не оступиться.");
            storyRect.y += storyGap;
            DrawNarrativeLine(storyRect, narrative, narrativeKey, keyTracking, accentWeight,
                "КРУТОЙ СКЛОН", " — ", "SHIFT + SPACE", ".");
            storyRect.y += storyGap;
            DrawNarrativeLine(storyRect, narrative, narrativeKey, keyTracking, accentWeight,
                "ДОЖДЬ", " — держи камень крепче, ", "С + SPACE", ".");
            storyRect.y += storyGap;
            DrawNarrativeLine(storyRect, narrative, narrativeKey, keyTracking, accentWeight,
                "ВЕТЕР", " — замри и ", "НЕ ТРОГАЙ КЛАВИШИ", ".");
            PassiveLabel(new Rect(tablet.x + 52f * scale, tablet.y + 250f * scale, tablet.width - 104f * scale, 82f * scale),
                "Неверные действия, идущие против природы, отнимают силы Сизифа. Как только силы иссякнут, Сизиф упадёт, и камень скатится к подножью горы.", tabletFinal);

            float buttonW = Mathf.Min(contentW * 0.58f, 440f * scale);
            float buttonX = (Screen.width - buttonW) * 0.5f;
            var startButton = new GUIStyle(btn)
            {
                font = uiStrongFont,
                fontSize = Mathf.Max(17, Mathf.RoundToInt(19f * scale)),
                fontStyle = FontStyle.Normal
            };
            if (GUI.Button(new Rect(buttonX, top + 512f * scale, buttonW, 50f * scale), "SPACE — НАЧАТЬ", startButton)) game.StartGame();

            float authorMargin = 24f * scale;
            float logoSize = 44f * scale;
            float authorWidth = 260f * scale;
            float authorX = Screen.width - authorMargin - authorWidth;
            float authorY = Screen.height - authorMargin - logoSize;
            var authorStyle = PassiveText(new GUIStyle(hint)
            {
                font = uiFont,
                fontSize = Mathf.Max(12, Mathf.RoundToInt(14f * scale)),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.72f, 0.66f, 0.53f, 0.92f) }
            });
            GUI.DrawTexture(new Rect(authorX, authorY, logoSize, logoSize), authorLogo, ScaleMode.ScaleToFit, true);
            PassiveLabel(new Rect(authorX + 52f * scale, authorY, authorWidth - 52f * scale, logoSize),
                "Автор: Иван Меркурьв", authorStyle);
        }

        void DrawSettings()
        {
            Rect2(new Rect(0, 0, Screen.width, Screen.height), new Color(0.04f, 0.03f, 0.07f, 0.9f));
            float scale = UiScale;
            var s = game.Set;
            var r = Center(560f * scale, 430f * scale);
            var settingsTitle = new GUIStyle(h2) { fontSize = Mathf.RoundToInt(22f * scale) };
            var settingsButton = new GUIStyle(btn) { fontSize = Mathf.RoundToInt(16f * scale) };
            var resetButton = SecondaryButtonStyle(Mathf.RoundToInt(12f * scale));
            GUILayout.BeginArea(r);
            GUILayout.Label("НАСТРОЙКИ СЛОЖНОСТИ", settingsTitle, GUILayout.Height(34f * scale));
            GUILayout.Space(14f * scale);
            s.DrainMul = Slider("Расход сил", s.DrainMul, 0.5f, 2f, "×" + s.DrainMul.ToString("0.0"), scale);
            s.FreqMul = Slider("Частота явлений", s.FreqMul, 0.5f, 2f, "×" + s.FreqMul.ToString("0.0"), scale);
            s.WindDurMul = Slider("Длительность ветра", s.WindDurMul, 0.5f, 2f, "×" + s.WindDurMul.ToString("0.0"), scale);
            s.RainProb = Slider("Вероятность дождя", s.RainProb, 0f, 3f, RainLabel(s.RainProb), scale);
            s.SteepMul = Slider("Крутизна склонов", s.SteepMul, 0.5f, 2f, "×" + s.SteepMul.ToString("0.0"), scale);
            GUILayout.Space(12f * scale);
            if (GUILayout.Button("НАЗАД", settingsButton, GUILayout.Height(38f * scale))) { s.Save(); game.CloseSettings(); }
            GUILayout.Space(7f * scale);
            if (GUILayout.Button("СБРОС", resetButton, GUILayout.Height(26f * scale))) { s.Reset(); s.Save(); }
            GUILayout.EndArea();
        }

        float Slider(string name, float val, float lo, float hi, string valText, float scale)
        {
            var labelStyle = PassiveText(new GUIStyle(body) { fontSize = Mathf.RoundToInt(14f * scale) });
            var valueStyle = PassiveText(new GUIStyle(labelStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(1f, 0.8f, 0.2f) }
            });
            var sliderStyle = new GUIStyle(GUI.skin.horizontalSlider) { fixedHeight = 8f * scale };
            var thumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb)
            {
                fixedWidth = 18f * scale,
                fixedHeight = 22f * scale
            };
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, labelStyle, GUILayout.Width(220f * scale), GUILayout.Height(24f * scale));
            GUILayout.Label(valText, valueStyle, GUILayout.Width(90f * scale), GUILayout.Height(24f * scale));
            GUILayout.EndHorizontal();
            float result = GUILayout.HorizontalSlider(val, lo, hi, sliderStyle, thumbStyle, GUILayout.Height(24f * scale));
            GUILayout.Space(5f * scale);
            return result;
        }

        static string RainLabel(float v) => v == 0 ? "выкл" : v < 0.75f ? "редко" : v <= 1.25f ? "обычная" : v <= 2f ? "часто" : "очень часто";

        void DrawOver()
        {
            Rect2(new Rect(0, 0, Screen.width, Screen.height), new Color(0.04f, 0.03f, 0.07f, 0.82f));
            float scale = UiScale;
            float width = Mathf.Min(480f * scale, Screen.width * 0.62f);
            btn.fontSize = Mathf.Max(17, Mathf.RoundToInt(20f * scale));
            float x = (Screen.width - width) * 0.5f;
            float titleWidth = Mathf.Min(760f * scale, Screen.width * 0.90f);
            float titleX = (Screen.width - titleWidth) * 0.5f;
            float top = Mathf.Max(18f, Screen.height * 0.08f);
            var overTitle = new GUIStyle(h2)
            {
                font = displayFont,
                fontSize = Mathf.Max(36, Mathf.RoundToInt(44f * scale)),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Overflow,
                normal = { textColor = title.normal.textColor }
            };
            var overReason = new GUIStyle(hint)
            {
                font = uiFont,
                fontSize = Mathf.Max(15, Mathf.RoundToInt(18f * scale)),
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.76f, 0.73f, 0.68f) }
            };
            var overHeightLabel = new GUIStyle(body)
            {
                font = displayFont,
                fontSize = Mathf.Max(27, Mathf.RoundToInt(30f * scale)),
                fontStyle = FontStyle.Normal,
                normal = { textColor = new Color(1f, 0.8f, 0.2f) }
            };
            var overHeightValue = new GUIStyle(body)
            {
                font = displayFont,
                fontSize = Mathf.Max(27, Mathf.RoundToInt(30f * scale)),
                fontStyle = FontStyle.Normal,
                normal = { textColor = new Color(1f, 0.8f, 0.2f) }
            };
            var overRecord = new GUIStyle(hint)
            {
                font = uiFont,
                fontSize = Mathf.RoundToInt(17f * scale),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.72f, 0.69f, 0.62f) }
            };

            PassiveLabel(new Rect(titleX, top, titleWidth, 72f * scale),
                game.FallReason == "slip" ? "СИЗИФ ПОСКОЛЬЗНУЛСЯ" : "СИЛЫ ИССЯКЛИ", overTitle);
            DrawTitleDivider(Screen.width * 0.5f, top + 64f * scale,
                Mathf.Min(170f * scale, titleWidth * 0.30f), scale);
            PassiveLabel(new Rect(titleX, top + 78f * scale, titleWidth, 42f * scale),
                game.FallReason == "slip" ? "Камень вырвался — и покатился вниз." : "Руки опустились — камень скатился к подножию.", overReason);
            DrawMetric(new Rect(x, top + 126f * scale, width, 50f * scale),
                "ВЫСОТА", Roman.To(game.Height), overHeightLabel, overHeightValue,
                2f * scale, 2f * scale, 10f * scale, false);
            TrackedLabel(new Rect(x, top + 180f * scale, width, 30f * scale), "РЕКОРД  " + Roman.To(game.Best), overRecord, 1.5f * scale);
            if (game.IsRecordScreen)
                PassiveLabel(new Rect(x, top + 208f * scale, width, 30f * scale), "★ НОВЫЙ РЕКОРД ★",
                    new GUIStyle(overRecord) { normal = { textColor = new Color(1f, 0.8f, 0.2f) } });

            Rect defeatQuoteRect = QuoteRect(DefeatQuote, 1f);
            defeatQuoteRect.y = top + 236f * scale;
            DrawStoneQuoteAt(defeatQuoteRect, DefeatQuote, 1f);

            float actionsY = defeatQuoteRect.yMax + 18f * scale;
            if (GUI.Button(new Rect(x, actionsY, width, 52f * scale), "SPACE — ЗАНОВО", btn)) game.StartGame();
            float secondaryOverW = width * 0.58f;
            float secondaryOverX = (Screen.width - secondaryOverW) * 0.5f;
            var secondaryOverBtn = SecondaryButtonStyle(Mathf.Max(14, Mathf.RoundToInt(16f * scale)));
            if (GUI.Button(new Rect(secondaryOverX, actionsY + 66f * scale, secondaryOverW, 36f * scale), "В МЕНЮ", secondaryOverBtn)) game.GoMenu();
        }
    }
}
