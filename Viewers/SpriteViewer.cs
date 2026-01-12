using System;
using System.IO;
using Eto.Drawing;
using Eto.Forms;
using Lin.Helper.Core.Sprite;
using SixLabors.ImageSharp.Formats.Png;
using PakViewer.Localization;

namespace PakViewer.Viewers
{
    /// <summary>
    /// SPR 精靈圖預覽器
    /// </summary>
    public class SpriteViewer : BaseViewer
    {
        private Drawable _drawable;
        private UITimer _timer;
        private SprFrame[] _frames;
        private int _frameIndex;

        public override string[] SupportedExtensions => new[] { ".spr" };

        public override void LoadData(byte[] data, string fileName)
        {
            _data = data;
            _fileName = fileName;

            _frames = SprReader.Load(data);
            if (_frames == null || _frames.Length == 0)
            {
                _control = new Label { Text = I18n.T("Error.LoadSpr") };
                return;
            }

            _frameIndex = 0;

            _drawable = new Drawable { BackgroundColor = Colors.Black };
            _drawable.Paint += OnPaint;

            _timer = new UITimer { Interval = 0.15 };
            _timer.Elapsed += OnTimerElapsed;

            if (_frames.Length > 1)
                _timer.Start();

            _control = _drawable;
        }

        private void OnTimerElapsed(object sender, EventArgs e)
        {
            _frameIndex = (_frameIndex + 1) % _frames.Length;
            _drawable?.Invalidate();
        }

        private void OnPaint(object sender, PaintEventArgs e)
        {
            if (_frames == null || _frameIndex >= _frames.Length) return;

            var frame = _frames[_frameIndex];
            if (frame.Image == null) return;

            using var ms = new MemoryStream();
            frame.Image.Save(ms, new PngEncoder());
            ms.Position = 0;
            using var bitmap = new Bitmap(ms);

            var scale = 2.0f;
            var x = (_drawable.Width - frame.Width * scale) / 2;
            var y = (_drawable.Height - frame.Height * scale) / 2;

            // 繪製紅色背景框 (精靈圖的範圍)
            e.Graphics.FillRectangle(Colors.DarkRed, (float)x, (float)y, frame.Width * scale, frame.Height * scale);

            // 繪製精靈圖
            e.Graphics.DrawImage(bitmap, (float)x, (float)y, frame.Width * scale, frame.Height * scale);

            var info = $"Frame {_frameIndex + 1}/{_frames.Length}  Size: {frame.Width}x{frame.Height}";
            e.Graphics.DrawText(new Font(SystemFont.Default), Colors.White, 10, 10, info);
        }

        public override void Dispose()
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;

            if (_frames != null)
            {
                foreach (var frame in _frames)
                    frame.Image?.Dispose();
                _frames = null;
            }

            base.Dispose();
        }
    }
}
