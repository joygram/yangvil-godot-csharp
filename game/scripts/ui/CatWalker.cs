using System;
using Godot;

// YangVil Unity 판 CatWalker.cs 이식. 좌표 = 마당 기준 왼쪽 위 원점, 아래로 +y.
namespace YangVillage.Ui
{
    // 마을 마당을 어슬렁거리는 고양이 한 마리.
    public partial class CatWalker : Control
    {
        const float Speed = 36;
        static readonly Random Rng = new Random();

        public Action Tapped;
        public Rect2 Bounds;
        public Vector2 CatSize;
        public Vector2 Spot;
        Vector2 _target;
        double _rest, _hop;
        const float HopHeight = 5;
        const float HopRate = 10;
        const float Spacing = 0.42f;
        const int Tries = 24;

        SpriteView _view;
        bool _flip;

        public static CatWalker Create(Control yard, string spriteId, float size, Rect2 bounds)
        {
            var walker = new CatWalker
            {
                Name = "cat",
                Size = new Vector2(size, size),
                CatSize = new Vector2(size, size),
                Bounds = bounds,
                MouseFilter = MouseFilterEnum.Stop,
            };
            yard.AddChild(walker);
            walker._view = SpriteView.Of(spriteId, size, walker);
            walker._view.PivotOffset = walker.CatSize / 2;
            return walker;
        }

        public void Place()
        {
            Spot = _target = FreePoint();
            _rest = Rng.NextDouble() * 2;
            Apply();
        }

        Vector2 RandomPoint() => new Vector2(
            Bounds.Position.X + (float)Rng.NextDouble() * Math.Max(1, Bounds.Size.X - CatSize.X),
            Bounds.Position.Y + (float)Rng.NextDouble() * Math.Max(1, Bounds.Size.Y - CatSize.Y));

        // 다른 고양이가 서 있거나 가려는 자리를 피해 고른다.
        Vector2 FreePoint()
        {
            var point = RandomPoint();
            for (var i = 0; i < Tries && Crowded(point); i++) point = RandomPoint();
            return point;
        }

        float Room(CatWalker other) => (CatSize.X + other.CatSize.X) * Spacing;

        bool Crowded(Vector2 point)
        {
            foreach (var node in GetParent().GetChildren())
                if (node is CatWalker other && other != this && (point + CatSize / 2).DistanceTo(other._target + other.CatSize / 2) < Room(other)) return true;
            return false;
        }

        // 지나가다 붙으면 서로 비켜선다.
        void StepAside(float delta)
        {
            foreach (var node in GetParent().GetChildren())
            {
                if (node is not CatWalker other || other == this) continue;
                var away = Spot + CatSize / 2 - other.Spot - other.CatSize / 2;
                if (away.Length() >= Room(other) || away == Vector2.Zero) continue;
                Spot += away.Normalized() * Speed * delta;
            }
            Spot = Spot.Clamp(Bounds.Position, Bounds.End - CatSize);
        }

        // 마당 정렬 기준.
        public float SortY => Spot.Y + CatSize.Y;

        void Apply()
        {
            Position = Spot;
            _view.FlipH = _flip;
        }

        public override void _Process(double delta)
        {
            var d = (float)delta;
            StepAside(d);
            Apply();
            if (_rest > 0)
            {
                _rest -= d;
                if (_rest <= 0)
                {
                    _target = FreePoint();
                    _flip = _target.X < Spot.X;
                    _hop = 0;
                }
                return;
            }
            _hop += d;
            Spot = Spot.MoveToward(_target, Speed * d);
            Apply();
            _view.RotationDegrees = Mathf.Sin((float)_hop * HopRate) * HopHeight;
            if (Spot.DistanceTo(_target) > 1) return;
            _rest = 1.5 + Rng.NextDouble() * 3;
            _view.Rotation = 0;
        }

        public override void _GuiInput(InputEvent e)
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) Tapped?.Invoke();
        }
    }
}
