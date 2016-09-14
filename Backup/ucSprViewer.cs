// Decompiled with JetBrains decompiler
// Type: PakViewer.ucSprViewer
// Assembly: PakViewer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 1B8FBB7F-36BB-4233-90DD-580453361518
// Assembly location: C:\Users\TonyQ\Downloads\PakViewer.exe

using PakViewer.Utility;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace PakViewer
{
  public class ucSprViewer : UserControl
  {
    private L1Spr.Frame[] Frames;
    private int imageid;
    private int Min_Yoffset;
    private int Min_Xoffset;
    private IContainer components;
    private Timer timer1;
    private PictureBox pictureBox1;
    private Label lblSprInfo;
    private TrackBar tbScale;

    [Browsable(false)]
    public L1Spr.Frame[] SprFrames
    {
      get
      {
        return this.Frames;
      }
      set
      {
        this.timer1.Stop();
        this.Frames = value;
        if (value == null || this.Frames.Length == 0)
          return;
        this.lblSprInfo.Text = string.Format("MaskColor : 0x{0:X4}", (object) this.Frames[0].maskcolor);
        this.pictureBox1.Image = (Image) null;
        this.Min_Yoffset = int.MaxValue;
        this.Min_Xoffset = int.MaxValue;
        this.imageid = 0;
        for (int index = 0; index < this.Frames.Length; ++index)
        {
          L1Spr.Frame frame = this.Frames[index];
          if (frame.image != null)
          {
            if (this.pictureBox1.Image == null)
              this.imageid = index;
            if (frame.y_offset < this.Min_Yoffset)
              this.Min_Yoffset = frame.y_offset;
            if (frame.x_offset < this.Min_Xoffset)
              this.Min_Xoffset = frame.x_offset;
          }
          this.ShowImage(this.Frames[this.imageid]);
        }
        this.lblSprInfo.Left = 10;
        this.lblSprInfo.Top = this.Height - this.lblSprInfo.Height - 10;
      }
    }

    public ucSprViewer()
    {
      this.InitializeComponent();
      this.timer1.Interval = 150;
    }

    public void Start()
    {
      this.timer1.Start();
    }

    public void Stop()
    {
      this.timer1.Stop();
    }

    private void timer1_Tick(object sender, EventArgs e)
    {
      if (this.Frames == null || this.Frames.Length == 0)
        return;
      this.imageid = ++this.imageid % this.Frames.Length;
      this.ShowImage(this.Frames[this.imageid]);
    }

    private void ShowImage(L1Spr.Frame frame)
    {
      if (frame.image == null)
        return;
      this.pictureBox1.Width = frame.width * this.tbScale.Value / 2;
      this.pictureBox1.Height = frame.height * this.tbScale.Value / 2;
      this.pictureBox1.Top = 20 + (frame.y_offset - this.Min_Yoffset) * this.tbScale.Value / 2;
      this.pictureBox1.Left = 70 + (frame.x_offset - this.Min_Xoffset) * this.tbScale.Value / 2;
      this.pictureBox1.Image = frame.image;
    }

    protected override void Dispose(bool disposing)
    {
      if (disposing && this.components != null)
        this.components.Dispose();
      base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
      this.components = (IContainer) new Container();
      this.timer1 = new Timer(this.components);
      this.pictureBox1 = new PictureBox();
      this.lblSprInfo = new Label();
      this.tbScale = new TrackBar();
      ((ISupportInitialize) this.pictureBox1).BeginInit();
      this.tbScale.BeginInit();
      this.SuspendLayout();
      this.timer1.Tick += new EventHandler(this.timer1_Tick);
      this.pictureBox1.Location = new Point(159, 169);
      this.pictureBox1.Name = "pictureBox1";
      this.pictureBox1.Size = new Size(100, 50);
      this.pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
      this.pictureBox1.TabIndex = 0;
      this.pictureBox1.TabStop = false;
      this.lblSprInfo.AutoSize = true;
      this.lblSprInfo.Font = new Font("細明體", 9f);
      this.lblSprInfo.Location = new Point(3, 409);
      this.lblSprInfo.Name = "lblSprInfo";
      this.lblSprInfo.Size = new Size(0, 12);
      this.lblSprInfo.TabIndex = 1;
      this.tbScale.BackColor = Color.LightGray;
      this.tbScale.LargeChange = 2;
      this.tbScale.Location = new Point(5, 3);
      this.tbScale.Minimum = 1;
      this.tbScale.Name = "tbScale";
      this.tbScale.Orientation = Orientation.Vertical;
      this.tbScale.Size = new Size(45, 180);
      this.tbScale.TabIndex = 2;
      this.tbScale.Value = 2;
      this.AutoScaleDimensions = new SizeF(6f, 12f);
      this.AutoScaleMode = AutoScaleMode.Font;
      this.BackColor = Color.Red;
      this.BorderStyle = BorderStyle.Fixed3D;
      this.Controls.Add((Control) this.lblSprInfo);
      this.Controls.Add((Control) this.tbScale);
      this.Controls.Add((Control) this.pictureBox1);
      this.Name = "ucSprViewer";
      this.Size = new Size(466, 424);
      ((ISupportInitialize) this.pictureBox1).EndInit();
      this.tbScale.EndInit();
      this.ResumeLayout(false);
      this.PerformLayout();
    }
  }
}
