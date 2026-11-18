using System.Collections.Generic;
using Godot;

// YangVil Unity 판 Sprites·SpriteView 이식. sprite 표 path(res://) 그대로 로드.
namespace YangVillage.Ui
{
    // sprite 테이블 → 프레임.
    public static class Sprites
    {
        static readonly Dictionary<string, Texture2D[]> Cache = new Dictionary<string, Texture2D[]>();

        public static Texture2D[] Frames(string spriteId)
        {
            if (Cache.TryGetValue(spriteId, out var frames)) return frames;
            var db = App.Db;
            var path = db.Get("sprite", "sprite_id", spriteId, "path");
            var sheet = GD.Load<Texture2D>(path);
            if (sheet == null) throw new KeyNotFoundException($"그림 없음: {path}");
            var start = db.Int("sprite", "sprite_id", spriteId, "start");
            frames = new Texture2D[db.Int("sprite", "sprite_id", spriteId, "count")];
            var columns = db.Int("sprite", "sprite_id", spriteId, "columns");
            var rows = db.Int("sprite", "sprite_id", spriteId, "rows");
            var cell = new Vector2((float)sheet.GetWidth() / columns, (float)sheet.GetHeight() / rows);
            for (var i = 0; i < frames.Length; i++)
            {
                var index = start + i;
                frames[i] = columns * rows == 1 ? sheet : new AtlasTexture
                {
                    Atlas = sheet,
                    Region = new Rect2(index % columns * cell.X, index / columns * cell.Y, cell),
                };
            }
            return Cache[spriteId] = frames;
        }

        public static Color ElementColor(string elementId) => Kit.Hex(App.Db.Get("element", "element_id", elementId, "color"));

        // 기운 표식. 그림 없는 기운은 null.
        public static SpriteView ElementIcon(string elementId, float size)
        {
            var spriteId = App.Db.Get("element", "element_id", elementId, "sprite_id");
            return spriteId == "" ? null : SpriteView.Of(spriteId, size);
        }
    }

    // sprite 한 개를 프레임 순서대로 보여 줌.
    public partial class SpriteView : TextureRect
    {
        const float FrameSeconds = 0.15f;
        Texture2D[] _frames = new Texture2D[0];
        float _time;
        bool _loop = true;

        public static SpriteView Of(string spriteId, float size, Node parent = null)
        {
            var view = new SpriteView
            {
                Name = spriteId,
                ExpandMode = ExpandModeEnum.IgnoreSize,
                StretchMode = StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(size, size),
                Size = new Vector2(size, size),
                MouseFilter = MouseFilterEnum.Ignore,
            };
            parent?.AddChild(view);
            view.Play(spriteId);
            return view;
        }

        public void Play(string spriteId, bool loop = true)
        {
            _frames = Sprites.Frames(spriteId);
            _loop = loop;
            _time = 0;
            Texture = _frames[0];
        }

        public override void _Process(double delta)
        {
            if (_frames.Length < 2) return;
            _time += (float)delta;
            var index = (int)(_time / FrameSeconds);
            Texture = _frames[_loop ? index % _frames.Length : Mathf.Min(index, _frames.Length - 1)];
        }
    }
}
