using System;
using System.Collections.Generic;
using Godot;

// YangVil Unity 판 Kit.cs 이식. UGUI 레이아웃 그룹 → Godot 컨테이너, 절차 생성 9-slice → StyleBoxFlat.
namespace YangVillage.Ui
{
    public static class Kit
    {
        public static readonly Color Ink = Hex("4a3728");
        public static readonly Color Paper = Hex("fff6e4");
        public static readonly Color Cream = Hex("ffe9c4");
        public static readonly Color Accent = Hex("ff9f6b");
        public static readonly Color AccentDark = Hex("d9713f");
        public static readonly Color Sky = Hex("bfe6f5");
        public static readonly Color Grass = Hex("a9d98a");
        public static readonly Color Night = Hex("2c2a4a");
        public static readonly Color Good = Hex("6fbf5f");
        public static readonly Color Bad = Hex("e2574c");
        public static readonly Color Muted = Hex("9a8b7c");
        public static readonly Color Gold = Hex("ffd935");

        const int Border = 3;

        public static Color Hex(string hex) => Color.FromHtml("#" + hex);

        public static Color Alpha(Color c, float a) => new Color(c.R, c.G, c.B, a);

        public static Color Lightened(Color c, float amount) => c.Lerp(Colors.White, amount);

        // 둥근 테두리 상자 (채움·테두리·반지름). 같은 조합은 재사용.
        static readonly Dictionary<(Color, Color, int, int), StyleBoxFlat> Boxes = new Dictionary<(Color, Color, int, int), StyleBoxFlat>();

        public static StyleBoxFlat Box(Color fill, Color border, int radius, int pad = 14)
        {
            var key = (fill, border, radius, pad);
            if (Boxes.TryGetValue(key, out var s)) return s;
            s = new StyleBoxFlat { BgColor = fill, BorderColor = border, AntiAliasing = true };
            s.SetBorderWidthAll(Border);
            s.SetCornerRadiusAll(radius);
            s.SetContentMarginAll(pad);
            return Boxes[key] = s;
        }

        public static T Add<T>(Node parent, T node) where T : Node
        {
            parent.AddChild(node);
            return node;
        }

        public static void Fill(Control c) => c.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        public static ColorRect Rect(Node parent, Color color, bool block = false)
        {
            var r = Add(parent, new ColorRect { Color = color, MouseFilter = block ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore });
            Fill(r);
            return r;
        }

        // 테두리 상자 패널 + 안쪽 여백.
        public static PanelContainer Panel(Node parent, Color fill, Color border, int radius = 16, int pad = 14)
        {
            var p = Add(parent, new PanelContainer());
            p.AddThemeStyleboxOverride("panel", Box(fill, border, radius, pad));
            return p;
        }

        public static VBoxContainer VBox(Node parent, int gap = 10)
        {
            var v = Add(parent, new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore });
            v.AddThemeConstantOverride("separation", gap);
            return v;
        }

        // 화면 세로 줄: 바깥 여백 margin, 칸 사이 gap.
        public static VBoxContainer Column(Node parent, int margin = 16, int gap = 10)
        {
            var m = Add(parent, new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore });
            Fill(m);
            foreach (var side in new[] { "left", "right", "top", "bottom" }) m.AddThemeConstantOverride("margin_" + side, margin);
            return VBox(m, gap);
        }

        public static HBoxContainer Row(Node parent, int gap = 8)
        {
            var h = Add(parent, new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center });
            h.AddThemeConstantOverride("separation", gap);
            return h;
        }

        public static void Expand(Control c, float ratio = 1)
        {
            c.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            c.SizeFlagsStretchRatio = ratio;
        }

        public static void Spacer(Node parent) => Add(parent, new Control { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ExpandFill });

        public static Label Label(Node parent, string text, int size = 20, Color? color = null)
        {
            var label = Add(parent, new Label
            {
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
            label.AddThemeFontSizeOverride("font_size", size);
            label.AddThemeColorOverride("font_color", color ?? Ink);
            return label;
        }

        static void Style(Button button, Color fill, Color hover, Color pressed, Color border, int radius)
        {
            button.AddThemeStyleboxOverride("normal", Box(fill, border, radius, 10));
            button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            button.AddThemeStyleboxOverride("hover", Box(hover, border, radius, 10));
            button.AddThemeStyleboxOverride("pressed", Box(pressed, border, radius, 10));
            button.AddThemeStyleboxOverride("disabled", Box(Cream, Muted, radius, 10));
        }

        // 버튼: 강조색 상자 + 가운데 글. 아이콘이 있으면 그림 위·글 아래.
        public static Button Button(Node parent, string text, Action onPress, int size = 20, Color? fill = null, Color? hover = null)
        {
            var f = fill ?? Accent;
            var button = Add(parent, new Button { Text = text, CustomMinimumSize = new Vector2(0, 44), AutowrapMode = TextServer.AutowrapMode.WordSmart });
            Style(button, f, hover ?? Lightened(f, 0.15f), AccentDark, Ink, 12);
            button.AddThemeFontSizeOverride("font_size", size);
            foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
                button.AddThemeColorOverride(state, Ink);
            button.AddThemeColorOverride("font_disabled_color", Muted);
            button.Pressed += onPress;
            Expand(button);
            return button;
        }

        public static void SetTextColor(Button button, Color color)
        {
            foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
                button.AddThemeColorOverride(state, color);
        }

        public static readonly Color Down = new Color(0.45f, 0.45f, 0.5f, 0.5f);

        // 화면 루트: 바탕색 채움.
        public static void Root(Control root, Color background) => Rect(root, background);

        // 종이 상자 + 안쪽 세로 줄.
        public static VBoxContainer Card(Node parent, int gap = 4) => VBox(Panel(parent, Paper, Ink, 16), gap);

        public static void CenterColumn(BoxContainer column) => column.Alignment = BoxContainer.AlignmentMode.Center;

        // 자리(왼쪽 위·크기), 회전·확대 축은 가운데.
        public static void PlaceCentered(Control c, Vector2 position, Vector2 size)
        {
            c.Position = position;
            c.Size = size;
            c.PivotOffset = size / 2;
        }

        public static void Outline(Label label, Color color, int size)
        {
            label.AddThemeColorOverride("font_outline_color", color);
            label.AddThemeConstantOverride("outline_size", size);
        }

        public static void NoWrap(Label label) => label.AutowrapMode = TextServer.AutowrapMode.Off;

        public static void SetDisabled(Button button, bool disabled) => button.Disabled = disabled;

        // 버튼 상자 색 바꾸기.
        public static void Restyle(Button button, Color fill, Color border, int radius = 12) => Style(button, fill, fill, fill, border, radius);

        public static string Stars(int stars)
        {
            var text = "";
            for (var i = 0; i < YangVillage.Battle.BattleState.MaxStars; i++) text += App.T(i < stars ? "ui_star_on" : "ui_star_off");
            return text;
        }

        // 버튼 글 위에 그림.
        public static void ButtonIcon(Button button, Texture2D texture, float size, bool above = true)
        {
            button.Icon = texture;
            button.ExpandIcon = true;
            button.AddThemeConstantOverride("icon_max_width", (int)size);
            if (above) button.VerticalIconAlignment = VerticalAlignment.Top;
            else button.IconAlignment = HorizontalAlignment.Left;
        }
    }
}
