using System.Collections.Generic;
using System.Linq;
using Godot;

// YangVil Unity 판 Fx.cs 이식 (EmoteFx·SlashFx·SparkFx·SkillCutIn). 그림 없이 도형으로 그리는 연출.
namespace YangVillage.Ui
{
    static class FxMath
    {
        public static float PosMod(float a, float b) { var r = a % b; return r < 0 ? r + b : r; }
        public static Color A(Color c, float a) => new Color(c.R, c.G, c.B, a);
        public static Color Darkened(Color c, float k) => new Color(c.R * (1 - k), c.G * (1 - k), c.B * (1 - k), c.A);

        public static void Ellipse(CanvasItem ci, Vector2 c, float rx, float ry, Color color, int segments = 32)
        {
            var points = new Vector2[segments];
            for (var i = 0; i < segments; i++)
            {
                var a = i * Mathf.Tau / segments;
                points[i] = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
            }
            ci.DrawColoredPolygon(points, color);
        }

        // 두 줄(바깥·안쪽)을 잇는 띠 — 오목한 모양(초승달) 용.
        public static void Strip(CanvasItem ci, IReadOnlyList<Vector2> outer, IReadOnlyList<Vector2> inner, Color color)
        {
            for (var i = 0; i + 1 < outer.Count; i++)
                ci.DrawColoredPolygon(new[] { outer[i], outer[i + 1], inner[i + 1], inner[i] }, color);
        }
    }

    // 감정 표시: 맞아서 찡그림(힘줄 표시), 쓰러진 눈(가위표)과 도는 별, 지친 땀과 헐떡이는 숨, 몸에 남은 불길.
    public partial class EmoteFx : Node2D
    {
        public enum Kind { Hurt, Dizzy, Sweat, Pant, Flame }

        const float HurtSeconds = 0.55f;
        const float FlameSeconds = 0.9f;
        static readonly Color Fire = Kit.Hex("ff7a2e");
        static readonly Color Water = Kit.Hex("7cc4ee");

        Kind _kind;
        float _size, _t, _facing;

        public static EmoteFx Of(Node parent, Kind kind, float size, Vector2 at, float facing = 1)
        {
            var fx = new EmoteFx { _kind = kind, _size = size, _facing = facing, Position = at, ZIndex = 5 };
            parent.AddChild(fx);
            return fx;
        }

        public override void _Process(double delta)
        {
            _t += (float)delta;
            if (_kind == Kind.Hurt && _t > HurtSeconds || _kind == Kind.Flame && _t > FlameSeconds) QueueFree();
            else QueueRedraw();
        }

        public override void _Draw()
        {
            if (_kind == Kind.Hurt) Vein();
            else if (_kind == Kind.Dizzy) Stars();
            else if (_kind == Kind.Pant) Puffs();
            else if (_kind == Kind.Flame) Flames();
            else Drop();
        }

        // 힘줄 표시: 가운데로 휜 호 넷.
        void Vein()
        {
            var pop = Mathf.Min(_t / 0.08f, 1) * (1 + 0.25f * Mathf.Max(0, 1 - _t / 0.2f));
            var fade = Mathf.Clamp((HurtSeconds - _t) / 0.2f, 0, 1);
            var reach = _size * pop;
            foreach (var (color, width) in new[] { (FxMath.A(Kit.Ink, fade), 0.34f), (FxMath.A(Kit.Bad, fade), 0.2f) })
                for (var i = 0; i < 4; i++)
                {
                    var angle = i * Mathf.Pi / 2 + Mathf.Pi / 4;
                    DrawArc(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach * 0.62f, reach * 0.4f, angle + Mathf.Pi - 0.95f, angle + Mathf.Pi + 0.95f, 8, color, reach * width);
                }
        }

        void Stars()
        {
            foreach (var (color, width) in new[] { (Kit.Paper, 0.3f), (Kit.Ink, 0.16f) })
                foreach (var side in new[] { -1, 1 })
                {
                    var eye = new Vector2(side * _size * 0.42f, _size * 0.75f);
                    var arm = _size * 0.24f;
                    DrawLine(eye - new Vector2(arm, arm), eye + new Vector2(arm, arm), color, _size * width);
                    DrawLine(eye - new Vector2(arm, -arm), eye + new Vector2(arm, -arm), color, _size * width);
                }
            for (var i = 0; i < 3; i++)
            {
                var angle = _t * 4.5f + i * Mathf.Tau / 3;
                var at = new Vector2(Mathf.Cos(angle) * _size, Mathf.Sin(angle) * _size * 0.32f);
                Star(at, _size * 0.4f, Kit.Ink);
                Star(at, _size * 0.28f, Kit.Gold);
            }
        }

        void Star(Vector2 at, float radius, Color color)
        {
            var points = new Vector2[10];
            for (var i = 0; i < 10; i++)
            {
                var angle = i * Mathf.Pi / 5 - Mathf.Pi / 2;
                points[i] = at + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * (i % 2 == 0 ? 1 : 0.45f);
            }
            DrawColoredPolygon(points, color);
        }

        // 헐떡임: 입 앞으로 숨이 짧게 끊겨 나온다.
        void Puffs()
        {
            for (var i = 0; i < 2; i++)
            {
                var life = FxMath.PosMod(_t / 0.42f + i * 0.5f, 1);
                var at = new Vector2(_facing * _size * (0.2f + life * 0.9f), -life * _size * 0.25f);
                var r = _size * (0.12f + life * 0.2f);
                DrawCircle(at, r * 1.25f, FxMath.A(Kit.Ink, (1 - life) * 0.8f));
                DrawCircle(at, r, FxMath.A(Kit.Paper, 1 - life));
            }
        }

        // 불길: 몸 여기저기서 혀가 일어 올랐다 꺼진다.
        void Flames()
        {
            var fade = Mathf.Clamp((FlameSeconds - _t) / 0.3f, 0, 1);
            foreach (var (color, grow) in new[] { (Fire, 1f), (Kit.Gold, 0.55f) })
                for (var i = 0; i < 7; i++)
                {
                    var life = FxMath.PosMod(_t * 1.9f + i * 0.37f, 1);
                    var at = new Vector2((FxMath.PosMod(i * 0.618f, 1) - 0.5f) * _size * 0.8f + Mathf.Sin(_t * 14 + i) * _size * 0.03f, -(0.1f + life * 0.75f) * _size);
                    var r = _size * 0.11f * (1 - life * 0.7f) * grow;
                    var tone = FxMath.A(color, fade * (1 - life * 0.6f));
                    DrawCircle(at, r, tone);
                    DrawColoredPolygon(new[] { at + new Vector2(-r * 0.9f, -r * 0.3f), at + new Vector2(0, -r * 3f), at + new Vector2(r * 0.9f, -r * 0.3f) }, tone);
                }
        }

        void Drop()
        {
            var at = new Vector2(0, FxMath.PosMod(_t, 0.9f) / 0.9f * _size * 0.8f);
            foreach (var (color, grow) in new[] { (Kit.Ink, 1.35f), (Water, 1f) })
            {
                var r = _size * 0.24f * grow;
                DrawCircle(at, r, color);
                DrawColoredPolygon(new[] { at + new Vector2(-r * 0.9f, -r * 0.3f), at + new Vector2(0, -r * 2.4f), at + new Vector2(r * 0.9f, -r * 0.3f) }, color);
            }
        }
    }

    // 베기 궤적: 가운데가 두껍고 끝이 뾰족한 초승달이 휘둘러지며 사라진다.
    public partial class SlashFx : Node2D
    {
        const int Segments = 18;
        const float HalfArc = 0.6f;

        public float Radius;
        public Color Tint;
        float _alpha = 1;

        void Crescent(float thickness, Color c)
        {
            var outer = new Vector2[Segments + 1];
            var inner = new Vector2[Segments + 1];
            for (var i = 0; i <= Segments; i++)
            {
                var t = i / (float)Segments;
                var angle = Mathf.Lerp(-HalfArc, HalfArc, t);
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                outer[i] = dir * Radius;
                inner[i] = dir * (Radius - thickness * Mathf.Sin(t * Mathf.Pi) - 0.5f);
            }
            FxMath.Strip(this, outer, inner, FxMath.A(c, c.A * _alpha));
        }

        public override void _Draw()
        {
            Crescent(Radius * 0.42f, FxMath.A(Tint, 0.85f));
            Crescent(Radius * 0.2f, Colors.White);
        }

        // pivot = 휘두르는 쪽 몸, facing = 대상 방향. 궤적이 대상을 가로질러 쓸고 지나간다.
        public static void Play(Node parent, Vector2 pivot, float radius, float facing, int sweep, Color tint)
        {
            var fx = new SlashFx { Radius = radius, Tint = tint, Position = pivot, ZIndex = 1600, Rotation = facing - sweep * 0.95f };
            parent.AddChild(fx);
            var tw = fx.CreateTween();
            tw.TweenProperty(fx, "rotation", facing + sweep * 0.95f, 0.09f).SetTrans(Tween.TransitionType.Quart).SetEase(Tween.EaseType.Out);
            tw.TweenMethod(Callable.From<float>(v => { fx._alpha = v; fx.QueueRedraw(); }), 1f, 0f, 0.1f);
            tw.TweenCallback(Callable.From(fx.QueueFree));
        }
    }

    // 타격 불티: 사방으로 튀는 선 + 퍼지는 충격 고리.
    public partial class SparkFx : Node2D
    {
        const int Rays = 10;

        public float Radius;
        public Color Tint;
        float _t;

        void Advance(float t)
        {
            _t = t;
            QueueRedraw();
        }

        public override void _Draw()
        {
            var fade = 1 - _t;
            for (var i = 0; i < Rays; i++)
            {
                var angle = i * Mathf.Tau / Rays + Mathf.Sin(i * 12.9898f) * 0.35f;
                var reach = 0.7f + FxMath.PosMod(i * 0.37f, 0.5f);
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                DrawLine(dir * Radius * reach * (0.2f + 0.7f * _t), dir * Radius * reach * (0.45f + 0.75f * _t), FxMath.A(Tint.Lerp(Colors.White, 0.5f), fade), 2 + 5 * fade);
            }
            DrawArc(Vector2.Zero, Radius * (0.3f + 0.7f * _t), 0, Mathf.Tau, 40, FxMath.A(Colors.White, fade * 0.9f), 1 + 9 * fade);
            DrawCircle(Vector2.Zero, Radius * 0.35f * fade * fade, FxMath.A(Colors.White, fade));
        }

        public static void Play(Node parent, Vector2 at, float radius, Color tint, float seconds = 0.24f)
        {
            var fx = new SparkFx { Radius = radius, Tint = tint, Position = at, ZIndex = 1550 };
            parent.AddChild(fx);
            fx.Advance(0);
            var tw = fx.CreateTween();
            tw.TweenMethod(Callable.From<float>(fx.Advance), 0f, 1f, seconds).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tw.TweenCallback(Callable.From(fx.QueueFree));
        }
    }

    // 필살 컷인: 비스듬한 띠가 열리고 그 안으로 이름이 속도선을 타고 지나간다.
    public partial class SkillCutIn : Node2D
    {
        const float BandHeight = 250;
        const float Slant = 60;
        const float Open = 0.12f;
        const int Lines = 16;

        Vector2 _area;
        Color _tint;
        Label _name;
        float _t;

        void Advance(float t)
        {
            _t = t;
            var w = _area.X;
            var enter = Mathf.Clamp(t / 0.16f, 0, 1);
            var leave = Mathf.Clamp((t - 0.84f) / 0.16f, 0, 1);
            var drift = (t - 0.5f) * 70;
            _name.Position = new Vector2(Mathf.Lerp(w, w * 0.05f, 1 - Mathf.Pow(1 - enter, 3)) - drift * 0.6f - leave * leave * w * 1.2f, _area.Y * 0.42f - 50);
            QueueRedraw();
        }

        public override void _Draw()
        {
            var w = _area.X;
            var mid = _area.Y * 0.42f;
            var open = Mathf.Min(Mathf.Clamp(_t / Open, 0, 1), Mathf.Clamp((1 - _t) / Open, 0, 1));
            DrawRect(new Rect2(Vector2.Zero, _area), FxMath.A(Kit.Night, 0.55f * open));
            var half = BandHeight / 2 * open;
            var band = new[] { new Vector2(0, mid - half + Slant), new Vector2(w, mid - half - Slant), new Vector2(w, mid + half - Slant), new Vector2(0, mid + half + Slant) };
            if (half > 0.01f)
                DrawPolygon(band, new[] { FxMath.Darkened(_tint, 0.55f), FxMath.Darkened(_tint, 0.2f), FxMath.Darkened(_tint, 0.35f), FxMath.Darkened(_tint, 0.7f) });
            for (var i = 0; i < Lines; i++)
            {
                var row = (i + 0.5f) / Lines * 2 - 1;
                var length = 90 + FxMath.PosMod(i * 53f, 140f);
                var x = FxMath.PosMod(i * 197f + _t * (1800 + i * 90), w + length) - length;
                var y = mid + row * half - (x / w * 2 - 1) * Slant;
                DrawLine(new Vector2(x, y), new Vector2(x + length, y - length / w * 2 * Slant), FxMath.A(Colors.White, 0.35f * open), 2 + i % 3);
            }
            DrawLine(band[0], band[1], FxMath.A(Colors.White, open), 5);
            DrawLine(band[3], band[2], FxMath.A(Colors.White, open), 5);
        }

        // parent = 화면 왼쪽 위 원점 노드.
        public static void Play(Node parent, Vector2 area, string caption, Color tint, float seconds)
        {
            var fx = new SkillCutIn { _area = area, _tint = tint, ZIndex = 3500 };
            parent.AddChild(fx);
            fx._name = Kit.Label(fx, caption, 38, Colors.White);
            Kit.Outline(fx._name, Kit.Ink, 12);
            Kit.NoWrap(fx._name);
            fx._name.Size = new Vector2(area.X * 0.9f, 100);
            fx.Advance(0);
            var tw = fx.CreateTween();
            tw.TweenMethod(Callable.From<float>(fx.Advance), 0f, 1f, seconds);
            tw.TweenCallback(Callable.From(fx.QueueFree));
        }
    }
}
