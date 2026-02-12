using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using Lin.Helper.Core.Sprite;
using SixLabors.ImageSharp.Formats.Png;
using PakViewer.Localization;

namespace PakViewer.Viewers
{
    /// <summary>
    /// SPR/SPX/SP2 精靈圖預覽器
    /// </summary>
    public class SpriteViewer : BaseViewer
    {
        private Drawable _drawable;
        private UITimer _timer;
        private SprFrame[] _frames;
        private int _frameIndex;

        // SP2 多方向支援
        private Dictionary<int, SprFrame[]> _directionFrames;
        private DropDown _dirDropDown;

        // 控制項
        private Button _pauseBtn;
        private DropDown _scaleDropDown;
        private DropDown _bgColorDropDown;
        private Label _frameInfoLabel;

        // 狀態
        private bool _isPaused;
        private float _scale = 2.0f;
        private Color _bgColor = Colors.Red;

        public override string[] SupportedExtensions => new[] { ".spr", ".spx", ".sp2" };

        public override void LoadData(byte[] data, string fileName)
        {
            _data = data;
            _fileName = fileName;

            var ext = Path.GetExtension(fileName)?.ToLower();

            try
            {
                if (ext == ".spx")
                {
                    _frames = L1SPX.Read(data);
                }
                else if (ext == ".sp2")
                {
                    _directionFrames = L1SP2.Read(data);
                    if (_directionFrames.Count > 0)
                        _frames = _directionFrames[_directionFrames.Keys.Min()];
                }
                else
                {
                    _frames = SprReader.Load(data);
                }
            }
            catch (Exception ex)
            {
                _control = new Label { Text = $"{I18n.T("Error.LoadSpr")}: {ex.Message}" };
                return;
            }

            if (_frames == null || _frames.Length == 0)
            {
                _control = new Label { Text = I18n.T("Error.LoadSpr") };
                return;
            }

            _frameIndex = 0;

            // 建立 Drawable
            _drawable = new Drawable { BackgroundColor = Colors.Black };
            _drawable.Paint += OnPaint;

            // 建立計時器
            _timer = new UITimer { Interval = 0.15 };
            _timer.Elapsed += OnTimerElapsed;

            if (_frames.Length > 1)
                _timer.Start();

            // 建立控制工具列
            _frameInfoLabel = new Label
            {
                TextColor = Colors.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            UpdateFrameInfo();

            _pauseBtn = new Button { Text = _frames.Length > 1 ? I18n.T("Button.Pause") : I18n.T("Button.Play") };
            _pauseBtn.Click += OnPauseClick;

            _scaleDropDown = new DropDown();
            _scaleDropDown.Items.Add("1x");
            _scaleDropDown.Items.Add("2x");
            _scaleDropDown.Items.Add("3x");
            _scaleDropDown.Items.Add("4x");
            _scaleDropDown.SelectedIndex = 1;  // 預設 2x
            _scaleDropDown.SelectedIndexChanged += OnScaleChanged;

            _bgColorDropDown = new DropDown();
            _bgColorDropDown.Items.Add(I18n.T("Color.Red"));
            _bgColorDropDown.Items.Add(I18n.T("Color.Green"));
            _bgColorDropDown.Items.Add(I18n.T("Color.Blue"));
            _bgColorDropDown.Items.Add(I18n.T("Color.Black"));
            _bgColorDropDown.Items.Add(I18n.T("Color.White"));
            _bgColorDropDown.Items.Add(I18n.T("Color.Gray"));
            _bgColorDropDown.SelectedIndex = 0;  // 預設紅色
            _bgColorDropDown.SelectedIndexChanged += OnBgColorChanged;

            var toolbar = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Padding = new Padding(5),
                VerticalContentAlignment = VerticalAlignment.Center,
                Items =
                {
                    _pauseBtn,
                    new Label { Text = I18n.T("Label.Scale"), VerticalAlignment = VerticalAlignment.Center },
                    _scaleDropDown,
                    new Label { Text = I18n.T("Label.Background"), VerticalAlignment = VerticalAlignment.Center },
                    _bgColorDropDown
                }
            };

            // SP2 方向選擇器
            if (_directionFrames != null && _directionFrames.Count > 1)
            {
                _dirDropDown = new DropDown();
                foreach (var dir in _directionFrames.Keys.OrderBy(k => k))
                {
                    _dirDropDown.Items.Add($"Dir {dir}");
                }
                _dirDropDown.SelectedIndex = 0;
                _dirDropDown.SelectedIndexChanged += OnDirectionChanged;

                toolbar.Items.Add(new Label { Text = "Dir:", VerticalAlignment = VerticalAlignment.Center });
                toolbar.Items.Add(_dirDropDown);
            }

            _control = new TableLayout
            {
                Rows =
                {
                    new TableRow(_drawable) { ScaleHeight = true },
                    new TableRow(toolbar)
                }
            };
        }

        private void UpdateFrameInfo()
        {
            if (_frames != null && _frameIndex < _frames.Length)
            {
                var frame = _frames[_frameIndex];
                _frameInfoLabel.Text = $"Frame {_frameIndex} ({frame.Width}x{frame.Height})";
            }
        }

        private void OnDirectionChanged(object sender, EventArgs e)
        {
            if (_directionFrames == null || _dirDropDown == null) return;

            var dirs = _directionFrames.Keys.OrderBy(k => k).ToList();
            int idx = _dirDropDown.SelectedIndex;
            if (idx < 0 || idx >= dirs.Count) return;

            // 清理舊 frames 的 Image
            DisposeFrames();

            _frames = _directionFrames[dirs[idx]];
            _frameIndex = 0;

            if (_frames.Length > 1 && !_isPaused)
                _timer?.Start();
            else
                _timer?.Stop();

            _pauseBtn.Text = _frames.Length > 1 ? I18n.T("Button.Pause") : I18n.T("Button.Play");
            _isPaused = false;
            UpdateFrameInfo();
            _drawable?.Invalidate();
        }

        private void OnPauseClick(object sender, EventArgs e)
        {
            _isPaused = !_isPaused;
            if (_isPaused)
            {
                _timer?.Stop();
                _pauseBtn.Text = I18n.T("Button.Play");
            }
            else
            {
                _timer?.Start();
                _pauseBtn.Text = I18n.T("Button.Pause");
            }
        }

        private void OnScaleChanged(object sender, EventArgs e)
        {
            var selected = _scaleDropDown.SelectedIndex;
            _scale = selected switch
            {
                0 => 1.0f,
                1 => 2.0f,
                2 => 3.0f,
                3 => 4.0f,
                _ => 2.0f
            };
            _drawable?.Invalidate();
        }

        private void OnBgColorChanged(object sender, EventArgs e)
        {
            var selected = _bgColorDropDown.SelectedIndex;
            _bgColor = selected switch
            {
                0 => Colors.Red,
                1 => Colors.Green,
                2 => Colors.Blue,
                3 => Colors.Black,
                4 => Colors.White,
                5 => Colors.Gray,
                _ => Colors.Red
            };
            _drawable?.Invalidate();
        }

        private void OnTimerElapsed(object sender, EventArgs e)
        {
            _frameIndex = (_frameIndex + 1) % _frames.Length;
            UpdateFrameInfo();
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

            var x = (_drawable.Width - frame.Width * _scale) / 2;
            var y = (_drawable.Height - frame.Height * _scale) / 2;

            // 繪製背景框 (精靈圖的範圍)
            e.Graphics.FillRectangle(_bgColor, (float)x, (float)y, frame.Width * _scale, frame.Height * _scale);

            // 繪製精靈圖
            e.Graphics.DrawImage(bitmap, (float)x, (float)y, frame.Width * _scale, frame.Height * _scale);

            // 繪製幀資訊
            var info = $"Frame {_frameIndex}/{_frames.Length} ({frame.Width}x{frame.Height})";
            e.Graphics.DrawText(new Font(SystemFont.Default), _bgColor, (float)x, (float)y - 20, info);
        }

        private void DisposeFrames()
        {
            // SP2 的 frames 由 _directionFrames 持有，不在這裡 dispose
            if (_directionFrames != null) return;

            if (_frames != null)
            {
                foreach (var frame in _frames)
                    frame.Image?.Dispose();
            }
        }

        public override void Dispose()
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;

            if (_directionFrames != null)
            {
                foreach (var kv in _directionFrames)
                    foreach (var frame in kv.Value)
                        frame.Image?.Dispose();
                _directionFrames = null;
            }
            else if (_frames != null)
            {
                foreach (var frame in _frames)
                    frame.Image?.Dispose();
            }
            _frames = null;

            base.Dispose();
        }
    }
}
