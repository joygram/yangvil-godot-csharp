using Godot;
using YangVillage.Battle;

// YangVil Unity 판 BattleActor.cs 이식 (BattleActor·BattleBackdrop).
namespace YangVillage.Ui
{
    // 전장에 선 유닛 하나: 그림자 · 그림 · 머리 위 체력/게이지 · 이름 · 지목 표시.
    public partial class BattleActor : Node2D
    {
        const float BarWidth = 84;
        const float BarHeight = 9;
        const float GaugeHeight = 6;
        static readonly Color Shadow = new Color(0.36f, 0.3f, 0.21f, 0.22f);
        static readonly Color Flashed = new Color(8, 8, 8);
        static readonly Color Pain = new Color(2.4f, 0.45f, 0.45f, 0.55f);
        static readonly Color Scorch = new Color(3f, 1.3f, 0.3f, 0.6f);
        // 지쳐 주저앉은 몸: 색은 조금만 빼고 알아볼 수 있게 둔다.
        static readonly Color Fainted = new Color(0.72f, 0.72f, 0.78f);

        public BattleUnit Unit;
        public Vector2 Home;
        public float Head;
        Sprite2D _sprite;
        Node2D _body;
        Label _focus;
        Vector2 _baseScale;
        bool _down;
        EmoteFx _sweat, _pant;
        Tween _idle;
        Hud _hud;

        public static BattleActor Of(Node parent, BattleUnit unit, Vector2 home, float height)
        {
            var actor = new BattleActor { Name = unit.Uid, Unit = unit, Home = home, Position = home, Head = height, ZIndex = 300 + (int)home.Y };
            parent.AddChild(actor);

            // 바닥 그림자·체력 막대 (자식보다 아래).
            actor._hud = new Hud { Actor = actor };
            actor.AddChild(actor._hud);

            var texture = Sprites.Frames(unit.SpriteId)[0];
            var scale = height / texture.GetHeight();
            actor._baseScale = new Vector2(scale, scale);
            actor._body = new Node2D { Name = "body" };
            actor.AddChild(actor._body);
            actor._sprite = new Sprite2D { Texture = texture, Scale = actor._baseScale, Offset = new Vector2(0, -texture.GetHeight() / 2f), FlipH = !unit.IsAlly };
            actor._body.AddChild(actor._sprite);

            var icon = Sprites.ElementIcon(unit.ElementId, 26);
            if (icon != null)
            {
                actor.AddChild(icon);
                Kit.PlaceCentered(icon, new Vector2(-BarWidth / 2 - 28, -height - 28), new Vector2(26, 26));
            }
            var caption = unit.IsAlly ? App.T("ui_ally_caption", unit.Name, App.Game.StageLabel(unit.Level)) : unit.IsBoss ? App.T("ui_boss_caption", unit.Name) : unit.Name;
            var name = actor.AddLabel(caption, 14, Kit.Ink, new Vector2(-110, -height - 50), new Vector2(220, 20));
            Kit.NoWrap(name);
            Kit.Outline(name, Kit.Paper, 8);
            actor._focus = actor.AddLabel(App.T("ui_focus_mark"), 26, Kit.Bad, new Vector2(-20, -height - 84), new Vector2(40, 30));
            actor._focus.Visible = false;
            return actor;
        }

        // 자식 Label (위치 = 왼쪽 위, 크기). 회전·확대 축은 가운데.
        public Label AddLabel(string text, int size, Color color, Vector2 position, Vector2 box)
        {
            var label = Kit.Label(this, text, size, color);
            Kit.PlaceCentered(label, position, box);
            return label;
        }

        // 대기: 숨 쉬듯 살짝 눌렸다 펴진다. 유닛마다 박자가 조금씩 다르다.
        public override void _Ready()
        {
            Breathe(0.55f + GD.Randf() * 0.25f, new Vector2(1.03f, 0.96f));
        }

        void Breathe(float beat, Vector2 squash)
        {
            _idle?.Kill();
            _body.Scale = Vector2.One;
            _idle = CreateTween().SetLoops();
            _idle.TweenProperty(_body, "scale", squash, beat).SetTrans(Tween.TransitionType.Sine);
            _idle.TweenProperty(_body, "scale", Vector2.One, beat).SetTrans(Tween.TransitionType.Sine);
        }

        partial class Hud : Node2D
        {
            public BattleActor Actor;

            public override void _Draw()
            {
                var a = Actor;
                FxMath.Ellipse(this, Vector2.Zero, a.Head * 0.42f, a.Head * 0.42f * 0.28f, Shadow);
                var top = -a.Head - 26;
                Bar(top, BarHeight, (float)a.Unit.Hp / a.Unit.MaxHp, a.Unit.IsAlly ? Kit.Good : Kit.Accent);
                if (a.Unit.IsAlly) Bar(top + BarHeight + 3, GaugeHeight, (float)a.Unit.Gauge / BattleUnit.GaugeMax, Kit.Gold);
            }

            void Bar(float y, float height, float ratio, Color fill)
            {
                var frame = new Rect2(-BarWidth / 2, y, BarWidth, height);
                DrawRect(frame.Grow(2), Kit.Ink);
                DrawRect(frame, Kit.Cream);
                DrawRect(new Rect2(frame.Position, new Vector2(BarWidth * Mathf.Clamp(ratio, 0, 1), height)), fill);
            }
        }

        // 등 쪽(-1 = 왼쪽).
        int Side => Unit.IsAlly ? -1 : 1;

        // 한마디: 고양이마다 정해진 줄이 말풍선으로 떴다 사라진다.
        void Bark(string prefix)
        {
            if (!Unit.IsAlly) return;
            var pick = 0;
            foreach (var letter in Unit.Name) pick += letter;
            var bubble = new Node2D { Name = "bubble", ZIndex = 20 };
            AddChild(bubble);
            var panel = Kit.Panel(bubble, Kit.Paper, Kit.Ink, 14, 6);
            panel.CustomMinimumSize = new Vector2(140, 0);
            panel.Position = new Vector2(-70, -(Head + 104));
            Kit.Label(panel, App.T(prefix + pick % App.Db.ConfigInt("bark_variants")), 16, Kit.Ink);
            var tw = bubble.CreateTween();
            tw.TweenInterval(1.1f);
            tw.TweenProperty(bubble, "modulate:a", 0f, 0.3f);
            tw.TweenCallback(Callable.From(bubble.QueueFree));
        }

        public Rect2 HitBox => new Rect2(Position + new Vector2(-Head * 0.5f, -Head), new Vector2(Head, Head));

        public void Refresh(bool focused)
        {
            _focus.Visible = focused && Unit.Alive;
            if (!Unit.Alive && !_down)
            {
                _down = true;
                _sprite.Modulate = Fainted;
                _sprite.CreateTween().TweenProperty(_sprite, "rotation", Mathf.DegToRad(Unit.IsAlly ? -90 : 90), 0.25f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
                _sweat?.QueueFree();
                _sweat = null;
                _pant?.QueueFree();
                _idle?.Kill();
                _body.Scale = Vector2.One;
                EmoteFx.Of(this, EmoteFx.Kind.Dizzy, Head * 0.3f, new Vector2(0, -Head * 0.75f));
                Bark("ui_bark_down_");
            }
            else if (Unit.Alive && _sweat == null && Unit.Hp * 100 < Unit.MaxHp * App.Db.ConfigInt("low_hp_pct"))
            {
                _sweat = EmoteFx.Of(this, EmoteFx.Kind.Sweat, Head * 0.3f, new Vector2(Side * Head * 0.42f, -Head * 0.8f));
                // 헐떡임: 숨이 가빠지고 입 앞으로 김이 나온다.
                _pant = EmoteFx.Of(this, EmoteFx.Kind.Pant, Head * 0.3f, new Vector2(-Side * Head * 0.38f, -Head * 0.5f), -Side);
                Breathe(0.13f, new Vector2(1.07f, 0.9f));
            }
            _hud.QueueRedraw();
        }

        // 돌진 직전: 몸을 낮춰 힘을 모은다.
        public void Crouch()
        {
            var tw = CreateTween();
            tw.TweenProperty(_sprite, "scale", _baseScale * new Vector2(1.15f, 0.82f), 0.09f);
            tw.TweenProperty(_sprite, "scale", _baseScale * new Vector2(0.92f, 1.1f), 0.05f);
            tw.TweenProperty(_sprite, "scale", _baseScale, 0.1f);
        }

        // 뛰어서 이동: 포물선으로 떴다가 착지하며 눌린다.
        public void Hop(float seconds, float height)
        {
            var tw = CreateTween();
            tw.TweenProperty(_sprite, "position:y", -height, seconds / 2).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(_sprite, "position:y", 0f, seconds / 2).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            tw.TweenProperty(_sprite, "scale", _baseScale * new Vector2(1.14f, 0.86f), 0.05f);
            tw.TweenProperty(_sprite, "scale", _baseScale, 0.1f);
        }

        // 걸어서 이동: 정해진 시간 동안 총총 뛴다.
        public void Walk(float seconds)
        {
            const float stride = 0.16f;
            var tw = CreateTween().SetLoops(Mathf.Max(1, (int)(seconds / stride)));
            tw.TweenProperty(_sprite, "position:y", -Head * 0.1f, stride / 2);
            tw.TweenProperty(_sprite, "position:y", 0f, stride / 2);
        }

        Sprite2D Copy()
        {
            return new Sprite2D { Texture = _sprite.Texture, Scale = _baseScale, Offset = _sprite.Offset, FlipH = _sprite.FlipH };
        }

        // 돌진 잔상.
        public Sprite2D Ghost(Node parent, Vector2 at)
        {
            var g = Copy();
            g.Position = at;
            g.ZIndex = ZIndex - 1;
            g.Modulate = new Color(1, 1, 1, 0.4f);
            parent.AddChild(g);
            return g;
        }

        // 내지르기: 뒤로 당겼다가 대상 쪽으로 튕겨 나가고 돌아온다. 타격은 StrikeLead 뒤에 맞춘다.
        public const float StrikeLead = 0.11f;

        public void Strike(Vector2 aim, float reach)
        {
            var lean = Mathf.DegToRad(aim.X >= 0 ? 16 : -16);
            var tw = CreateTween();
            tw.TweenProperty(_body, "position", -aim * reach * 0.5f, 0.07f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tw.Parallel().TweenProperty(_sprite, "rotation", -lean * 0.6f, 0.07f);
            tw.TweenProperty(_body, "position", aim * reach, 0.04f);
            tw.Parallel().TweenProperty(_sprite, "rotation", lean, 0.04f);
            tw.Parallel().TweenProperty(_sprite, "scale", _baseScale * new Vector2(1.2f, 0.88f), 0.04f);
            tw.TweenInterval(0.05f);
            tw.TweenProperty(_body, "position", Vector2.Zero, 0.14f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tw.Parallel().TweenProperty(_sprite, "rotation", 0f, 0.14f);
            tw.Parallel().TweenProperty(_sprite, "scale", _baseScale, 0.14f);
        }

        // 쏘는 반동: 대상 반대쪽으로 밀렸다 돌아온다.
        public void Recoil(Vector2 aim, float reach)
        {
            var tw = CreateTween();
            tw.TweenProperty(_body, "position", -aim * reach, 0.04f);
            tw.Parallel().TweenProperty(_sprite, "scale", _baseScale * new Vector2(0.9f, 1.1f), 0.04f);
            tw.TweenProperty(_body, "position", Vector2.Zero, 0.16f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tw.Parallel().TweenProperty(_sprite, "scale", _baseScale, 0.16f);
        }

        // 맞음: 하얗게 번쩍 + 맞은 방향으로 밀렸다 돌아옴. 아픈 잔영이 뒤로 밀려 나가고, 뜨거운 공격이면 불길이 남는다.
        public void Hurt(Vector2 blow, float push, bool burns = false)
        {
            var echo = Copy();
            echo.Rotation = _sprite.Rotation;
            echo.ZIndex = -1;
            echo.Modulate = burns ? Scorch : Pain;
            AddChild(echo);
            var et = echo.CreateTween();
            et.TweenProperty(echo, "position", blow * push * 2.4f, 0.34f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            et.Parallel().TweenProperty(echo, "scale", _baseScale * 1.12f, 0.34f);
            et.Parallel().TweenProperty(echo, "modulate:a", 0f, 0.34f);
            et.TweenCallback(Callable.From(echo.QueueFree));
            if (burns) EmoteFx.Of(this, EmoteFx.Kind.Flame, Head, Vector2.Zero);
            _sprite.Modulate = Flashed;
            var tw = CreateTween();
            tw.TweenProperty(_body, "position", blow * push, 0.04f);
            tw.Parallel().TweenProperty(_sprite, "scale", _baseScale * new Vector2(0.82f, 1.14f), 0.04f);
            tw.Parallel().TweenProperty(_sprite, "rotation", Mathf.DegToRad(blow.X >= 0 ? 10 : -10), 0.04f);
            tw.TweenProperty(_sprite, "modulate", Unit.Alive ? Colors.White : Fainted, 0.08f);
            if (Unit.Alive)
            {
                EmoteFx.Of(this, EmoteFx.Kind.Hurt, Head * 0.2f, new Vector2(Side * Head * 0.3f, -Head * 0.9f));
                if (GD.RandRange(0, 99) < App.Db.ConfigInt("bark_hurt_pct")) Bark("ui_bark_hurt_");
            }
            tw.TweenProperty(_body, "position", Vector2.Zero, 0.14f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tw.Parallel().TweenProperty(_sprite, "scale", _baseScale, 0.14f);
            if (Unit.Alive) tw.Parallel().TweenProperty(_sprite, "rotation", 0f, 0.14f);
        }

        // 힘 모으기처럼 대상 없는 행동.
        public void Glow()
        {
            _sprite.Modulate = Kit.Gold;
            CreateTween().TweenProperty(_sprite, "modulate", Colors.White, 0.3f);
        }
    }

    // 전투 배경 그림(무대와 함께 확대·흔들림) 또는 그 위에 고정으로 덮는 HUD 바탕.
    public partial class BattleBackdrop : Node2D
    {
        const float Bleed = 40;
        static readonly Color Clear = FxMath.A(Kit.Night, 0);
        static readonly Color TopShade = FxMath.A(Kit.Night, 0.7f);

        public Vector2 Area;
        public float FieldHeight;
        public Texture2D Picture;
        float _scroll;

        // 전진한 거리: 그림이 그만큼 뒤로 흐른다.
        public float Scroll
        {
            get => _scroll;
            set { _scroll = value; QueueRedraw(); }
        }

        void Fade(float top, float bottom, Color from, Color to) =>
            DrawPolygon(new[] { new Vector2(0, top), new Vector2(Area.X, top), new Vector2(Area.X, bottom), new Vector2(0, bottom) }, new[] { from, from, to, to });

        public override void _Draw()
        {
            if (Picture != null) { Tiles(); return; }
            Fade(0, FieldHeight * 0.16f, TopShade, Clear);
            Fade(FieldHeight * 0.9f, FieldHeight, Clear, Kit.Night);
            DrawRect(new Rect2(0, FieldHeight, Area.X, Area.Y - FieldHeight), Kit.Night);
        }

        // 흔들려도 가장자리가 비지 않게 사방으로 넘치게, 이음매가 맞게 한 장 걸러 뒤집는다.
        void Tiles()
        {
            var height = FieldHeight + Bleed * 2;
            var width = height * Picture.GetWidth() / Picture.GetHeight();
            var start = (Area.X - width) / 2 - _scroll;
            var first = Mathf.FloorToInt((-Bleed - start) / width);
            for (var n = first; start + n * width < Area.X + Bleed; n++)
            {
                var left = start + n * width;
                if (n % 2 != 0)
                {
                    DrawSetTransform(new Vector2(left + width, -Bleed), 0, new Vector2(-1, 1));
                    DrawTextureRect(Picture, new Rect2(0, 0, width, height), false);
                }
                else
                {
                    DrawSetTransform(Vector2.Zero);
                    DrawTextureRect(Picture, new Rect2(left, -Bleed, width, height), false);
                }
            }
            DrawSetTransform(Vector2.Zero);
        }
    }
}
