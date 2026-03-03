namespace Loupedeck.HomeAssistantByBatuPlugin
{
    using System;

    internal static class IconHelper
    {
        public static BitmapImage CreateEntityImage(
            PluginImageSize imageSize,
            String displayText,
            String stateText,
            Boolean isOn,
            String iconChar = null,
            Int32 brightnessPct = -1)
        {
            using var builder = new BitmapBuilder(imageSize);

            var bgColor = GetLightBgColor(isOn, brightnessPct);
            builder.Clear(bgColor);

            var textColor = GetTextColor(isOn, brightnessPct);

            if (!String.IsNullOrEmpty(iconChar))
            {
                builder.DrawText(iconChar, 0, 5, builder.Width, 30, textColor, 22);
            }

            var nameY = String.IsNullOrEmpty(iconChar) ? 10 : 32;
            var truncatedName = TruncateName(displayText);

            builder.DrawText(truncatedName, 2, nameY, builder.Width - 4, 24, textColor);

            if (!String.IsNullOrEmpty(stateText))
            {
                var stateColor = isOn
                    ? ScaleColor(180, 230, 180, brightnessPct)
                    : new BitmapColor(140, 140, 140);
                builder.DrawText(stateText, 2, nameY + 22, builder.Width - 4, 20, stateColor, 11);
            }

            return builder.ToImage();
        }

        public static BitmapImage CreateAdjustmentImage(
            PluginImageSize imageSize,
            String displayText,
            String valueText,
            Boolean isOn,
            Int32 brightnessPct = -1)
        {
            using var builder = new BitmapBuilder(imageSize);

            var bgColor = GetLightBgColor(isOn, brightnessPct);
            builder.Clear(bgColor);

            var textColor = GetTextColor(isOn, brightnessPct);
            var truncatedName = TruncateName(displayText);

            builder.DrawText(truncatedName, 2, 8, builder.Width - 4, 24, textColor);

            if (!String.IsNullOrEmpty(valueText))
            {
                var valueColor = isOn
                    ? GetValueColor(brightnessPct)
                    : new BitmapColor(120, 120, 120);
                builder.DrawText(valueText, 2, 34, builder.Width - 4, 28, valueColor, 16);
            }

            return builder.ToImage();
        }

        public static BitmapImage CreateColorTempImage(
            PluginImageSize imageSize,
            String displayText,
            String valueText,
            Boolean isOn,
            Double warmthFactor,
            Int32 brightnessPct = -1)
        {
            using var builder = new BitmapBuilder(imageSize);

            Int32 bgR, bgG, bgB;
            if (!isOn)
            {
                bgR = 35; bgG = 35; bgB = 35;
            }
            else
            {
                var intensity = brightnessPct >= 0 ? brightnessPct / 100.0 : 0.8;
                intensity = 0.25 + (intensity * 0.75);

                bgR = Math.Clamp((Int32)((180 + 75 * warmthFactor) * intensity), 0, 255);
                bgG = Math.Clamp((Int32)((160 + 50 * (1 - warmthFactor)) * intensity), 0, 255);
                bgB = Math.Clamp((Int32)((80 + 175 * (1 - warmthFactor)) * intensity), 0, 255);
            }

            builder.Clear(new BitmapColor(bgR, bgG, bgB));

            var luma = isOn ? (bgR * 0.299 + bgG * 0.587 + bgB * 0.114) : 0;
            var textColor = luma > 140 ? BitmapColor.Black : BitmapColor.White;

            var truncatedName = TruncateName(displayText);
            builder.DrawText(truncatedName, 2, 8, builder.Width - 4, 24, textColor);

            if (!String.IsNullOrEmpty(valueText))
            {
                var valueColor = luma > 140
                    ? new BitmapColor(30, 30, 30)
                    : new BitmapColor(220, 220, 220);
                builder.DrawText(valueText, 2, 34, builder.Width - 4, 28, valueColor, 16);
            }

            return builder.ToImage();
        }

        public static BitmapImage CreateOfflineImage(PluginImageSize imageSize)
        {
            using var builder = new BitmapBuilder(imageSize);
            builder.Clear(new BitmapColor(60, 15, 15));
            builder.DrawText("Home", 0, 14, builder.Width, 20, BitmapColor.White);
            builder.DrawText("Assistant", 0, 32, builder.Width, 20, BitmapColor.White);
            builder.DrawText("Offline", 0, 50, builder.Width, 16, new BitmapColor(255, 100, 100), 10);
            return builder.ToImage();
        }

        public static String GetDomainIcon(String domain)
        {
            return domain switch
            {
                "light" => "\u2600",
                "switch" => "\u26A1",
                "automation" => "\u2699",
                "scene" => "\u2B50",
                "script" => "\u25B6",
                "button" => "\u25CF",
                "lock" => "\uD83D\uDD12",
                "cover" => "\u2195",
                "climate" => "\uD83C\uDF21",
                "sensor" => "\uD83D\uDCCA",
                "binary_sensor" => "\u26AB",
                "fan" => "\uD83C\uDF00",
                "media_player" => "\u266B",
                "camera" => "\uD83D\uDCF7",
                "water_heater" => "\uD83D\uDCA7",
                _ => "\u2B24",
            };
        }

        private static BitmapColor GetLightBgColor(Boolean isOn, Int32 brightnessPct)
        {
            if (!isOn)
            {
                return new BitmapColor(35, 35, 35);
            }

            if (brightnessPct < 0)
            {
                return new BitmapColor(40, 100, 180);
            }

            var t = brightnessPct / 100.0;
            t = 0.15 + (t * 0.85);

            var r = (Int32)(255 * t);
            var g = (Int32)(190 * t);
            var b = (Int32)(50 * t);

            return new BitmapColor(
                Math.Clamp(r, 0, 255),
                Math.Clamp(g, 0, 255),
                Math.Clamp(b, 0, 255));
        }

        private static BitmapColor GetTextColor(Boolean isOn, Int32 brightnessPct)
        {
            if (!isOn)
            {
                return new BitmapColor(160, 160, 160);
            }

            if (brightnessPct >= 0 && brightnessPct > 60)
            {
                return BitmapColor.Black;
            }

            return BitmapColor.White;
        }

        private static BitmapColor GetValueColor(Int32 brightnessPct)
        {
            if (brightnessPct >= 0 && brightnessPct > 60)
            {
                return new BitmapColor(30, 30, 30);
            }

            return new BitmapColor(255, 255, 200);
        }

        private static BitmapColor ScaleColor(Int32 r, Int32 g, Int32 b, Int32 brightnessPct)
        {
            if (brightnessPct < 0)
            {
                return new BitmapColor(r, g, b);
            }

            var t = 0.4 + (brightnessPct / 100.0 * 0.6);
            return new BitmapColor(
                Math.Clamp((Int32)(r * t), 0, 255),
                Math.Clamp((Int32)(g * t), 0, 255),
                Math.Clamp((Int32)(b * t), 0, 255));
        }

        private static String TruncateName(String name)
        {
            if (String.IsNullOrEmpty(name))
            {
                return "";
            }

            return name.Length > 14 ? name.Substring(0, 12) + ".." : name;
        }
    }
}
