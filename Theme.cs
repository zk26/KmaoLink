using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace KmaoLink
{
    /// <summary>
    /// 亮色主题 - 清晰易读
    /// </summary>
    public static class Theme
    {
        // ===== 纯色背景 =====
        public static readonly Color BgMain = Color.White;
        public static readonly Color BgCard = Color.FromArgb(250, 250, 250);
        public static readonly Color BgCardHover = Color.FromArgb(242, 242, 242);
        public static readonly Color BgHeader = Color.FromArgb(247, 247, 247);
        public static readonly Color BgRecent = Color.FromArgb(233, 243, 255);
        public static readonly Color BgRecentHover = Color.FromArgb(222, 236, 252);

        // 强调色
        public static readonly Color Accent = Color.FromArgb(0, 120, 215);
        public static readonly Color AccentHover = Color.FromArgb(22, 132, 222);
        public static readonly Color AccentPressed = Color.FromArgb(0, 100, 190);

        // 状态色
        public static readonly Color Success = Color.FromArgb(16, 124, 16);
        public static readonly Color Danger = Color.FromArgb(200, 50, 40);
        public static readonly Color DangerHover = Color.FromArgb(215, 65, 55);

        // 文字
        public static readonly Color Text1 = Color.FromArgb(20, 20, 20);
        public static readonly Color Text2 = Color.FromArgb(90, 90, 90);
        public static readonly Color Text3 = Color.FromArgb(150, 150, 150);
        public static readonly Color TextWhite = Color.White;

        // 边框
        public static readonly Color Border = Color.FromArgb(222, 222, 222);
        public static readonly Color BorderCard = Color.FromArgb(232, 232, 232);

        // ===== 设备图标 =====
        public static (string icon, string type) GetDeviceIcon(string name)
        {
            string lower = name.ToLower();

            if (Any(lower, "buds", "airpod", "headphone", "headset", "earphone", "wh-", "wf-", "edifier", "freebuds"))
                return ("🎧", "耳机");
            if (Any(lower, "mic", "microphone", "dji"))
                return ("🎤", "麦克风");
            if (Any(lower, "keyboard", "kbd", "k380", "k480", "k580", "mx keys"))
                return ("⌨️", "键盘");
            if (Any(lower, "mouse", "mice", "mx master", "pebble"))
                return ("🖱️", "鼠标");
            if (Any(lower, "speaker", "sound", "jbl", "bose", "soundbox", "小爱"))
                return ("🔊", "音箱");
            if (Any(lower, "watch", "band", "手环"))
                return ("⌚", "手表");
            if (Any(lower, "controller", "gamepad", "xbox", "dualsense", "switch pro"))
                return ("🎮", "手柄");
            if (Any(lower, "printer", "print"))
                return ("🖨️", "打印机");
            if (Any(lower, "phone", "iphone", "pixel", "galaxy", "redmi", "xiaomi", "huawei", "honor"))
                return ("📱", "手机");
            if (Any(lower, "tv", "电视", "box"))
                return ("📺", "电视");

            return ("📶", "设备");
        }

        private static bool Any(string text, params string[] keywords)
        {
            foreach (var k in keywords)
                if (text.Contains(k)) return true;
            return false;
        }

        // ===== 圆角 =====
        public static GraphicsPath RoundRect(int x, int y, int w, int h, int r)
        {
            var path = new GraphicsPath();
            int d = r * 2;
            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + w - d, y, d, d, 270, 90);
            path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            path.AddArc(x, y + h - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Region RoundedRegion(Size size, int radius)
        {
            using var path = RoundRect(0, 0, size.Width - 1, size.Height - 1, radius);
            return new Region(path);
        }
    }
}
