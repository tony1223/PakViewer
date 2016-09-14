// Decompiled with JetBrains decompiler
// Type: PakViewer.ucTextCompare
// Assembly: PakViewer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 1B8FBB7F-36BB-4233-90DD-580453361518
// Assembly location: C:\Users\TonyQ\Downloads\PakViewer.exe

using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace PakViewer
{
  public class ucTextCompare : UserControl
  {
    private IContainer components;
    private SplitContainer splitContainer1;
    private ListBox lstSource;
    private ListBox lstTarget;
    private TextBox txtSource;
    private SplitContainer splitContainer2;
    private SplitContainer splitContainer3;
    private TextBox txtTarget;

    public string SourceText
    {
      set
      {
        this.lstSource.Items.Clear();
        string str1 = value;
        char[] chArray = new char[1]{ '\n' };
        foreach (string str2 in str1.Split(chArray))
          this.lstSource.Items.Add((object) str2.Replace("\r", ""));
      }
    }

    public string TargetText
    {
      set
      {
        this.lstTarget.Items.Clear();
        string str1 = value;
        char[] chArray = new char[1]{ '\n' };
        foreach (string str2 in str1.Split(chArray))
          this.lstTarget.Items.Add((object) str2.Replace("\r", ""));
      }
    }

    public ucTextCompare()
    {
      this.InitializeComponent();
    }

    private void lstSource_SelectedIndexChanged(object sender, EventArgs e)
    {
      if (this.lstSource.SelectedIndex >= this.lstTarget.Items.Count || this.lstTarget.SelectedIndex == this.lstSource.SelectedIndex)
        return;
      this.lstTarget.SelectedIndex = this.lstSource.SelectedIndex;
    }

    private void lstTarget_SelectedIndexChanged(object sender, EventArgs e)
    {
      if (this.lstSource.SelectedIndex >= this.lstTarget.Items.Count || this.lstTarget.SelectedIndex == this.lstSource.SelectedIndex)
        return;
      this.lstTarget.SelectedIndex = this.lstSource.SelectedIndex;
    }

    private void txtSource_TextChanged(object sender, EventArgs e)
    {
      int index = this.lstSource.FindString(((Control) sender).Text);
      if (index == -1)
        return;
      this.lstSource.SetSelected(index, true);
    }

    private void txtTarget_TextChanged(object sender, EventArgs e)
    {
      int index = this.lstTarget.FindString(((Control) sender).Text);
      if (index < 0)
        return;
      this.lstTarget.SetSelected(index, true);
    }

    protected override void Dispose(bool disposing)
    {
      if (disposing && this.components != null)
        this.components.Dispose();
      base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
      this.splitContainer1 = new SplitContainer();
      this.lstSource = new ListBox();
      this.lstTarget = new ListBox();
      this.txtSource = new TextBox();
      this.splitContainer2 = new SplitContainer();
      this.splitContainer3 = new SplitContainer();
      this.txtTarget = new TextBox();
      this.splitContainer1.Panel1.SuspendLayout();
      this.splitContainer1.Panel2.SuspendLayout();
      this.splitContainer1.SuspendLayout();
      this.splitContainer2.Panel1.SuspendLayout();
      this.splitContainer2.Panel2.SuspendLayout();
      this.splitContainer2.SuspendLayout();
      this.splitContainer3.Panel1.SuspendLayout();
      this.splitContainer3.Panel2.SuspendLayout();
      this.splitContainer3.SuspendLayout();
      this.SuspendLayout();
      this.splitContainer1.Dock = DockStyle.Fill;
      this.splitContainer1.Location = new Point(0, 0);
      this.splitContainer1.Name = "splitContainer1";
      this.splitContainer1.Panel1.Controls.Add((Control) this.splitContainer2);
      this.splitContainer1.Panel2.Controls.Add((Control) this.splitContainer3);
      this.splitContainer1.Size = new Size(626, 456);
      this.splitContainer1.SplitterDistance = 303;
      this.splitContainer1.TabIndex = 0;
      this.lstSource.Dock = DockStyle.Fill;
      this.lstSource.FormattingEnabled = true;
      this.lstSource.IntegralHeight = false;
      this.lstSource.ItemHeight = 12;
      this.lstSource.Location = new Point(0, 0);
      this.lstSource.Name = "lstSource";
      this.lstSource.ScrollAlwaysVisible = true;
      this.lstSource.Size = new Size(303, 427);
      this.lstSource.TabIndex = 0;
      this.lstSource.SelectedIndexChanged += new EventHandler(this.lstSource_SelectedIndexChanged);
      this.lstTarget.Dock = DockStyle.Fill;
      this.lstTarget.FormattingEnabled = true;
      this.lstTarget.IntegralHeight = false;
      this.lstTarget.ItemHeight = 12;
      this.lstTarget.Location = new Point(0, 0);
      this.lstTarget.Name = "lstTarget";
      this.lstTarget.ScrollAlwaysVisible = true;
      this.lstTarget.Size = new Size(319, 427);
      this.lstTarget.TabIndex = 0;
      this.lstTarget.SelectedIndexChanged += new EventHandler(this.lstTarget_SelectedIndexChanged);
      this.txtSource.Dock = DockStyle.Fill;
      this.txtSource.Location = new Point(0, 0);
      this.txtSource.Name = "txtSource";
      this.txtSource.Size = new Size(303, 22);
      this.txtSource.TabIndex = 2;
      this.txtSource.TextChanged += new EventHandler(this.txtSource_TextChanged);
      this.splitContainer2.Dock = DockStyle.Fill;
      this.splitContainer2.FixedPanel = FixedPanel.Panel2;
      this.splitContainer2.IsSplitterFixed = true;
      this.splitContainer2.Location = new Point(0, 0);
      this.splitContainer2.Name = "splitContainer2";
      this.splitContainer2.Orientation = Orientation.Horizontal;
      this.splitContainer2.Panel1.Controls.Add((Control) this.lstSource);
      this.splitContainer2.Panel2.Controls.Add((Control) this.txtSource);
      this.splitContainer2.Size = new Size(303, 456);
      this.splitContainer2.SplitterDistance = 427;
      this.splitContainer2.TabIndex = 2;
      this.splitContainer3.Dock = DockStyle.Fill;
      this.splitContainer3.FixedPanel = FixedPanel.Panel2;
      this.splitContainer3.IsSplitterFixed = true;
      this.splitContainer3.Location = new Point(0, 0);
      this.splitContainer3.Name = "splitContainer3";
      this.splitContainer3.Orientation = Orientation.Horizontal;
      this.splitContainer3.Panel1.Controls.Add((Control) this.lstTarget);
      this.splitContainer3.Panel2.Controls.Add((Control) this.txtTarget);
      this.splitContainer3.Size = new Size(319, 456);
      this.splitContainer3.SplitterDistance = 427;
      this.splitContainer3.TabIndex = 1;
      this.txtTarget.Dock = DockStyle.Fill;
      this.txtTarget.Location = new Point(0, 0);
      this.txtTarget.Name = "txtTarget";
      this.txtTarget.Size = new Size(319, 22);
      this.txtTarget.TabIndex = 0;
      this.txtTarget.TextChanged += new EventHandler(this.txtTarget_TextChanged);
      this.AutoScaleDimensions = new SizeF(6f, 12f);
      this.AutoScaleMode = AutoScaleMode.Font;
      this.Controls.Add((Control) this.splitContainer1);
      this.Name = "ucTextCompare";
      this.Size = new Size(626, 456);
      this.splitContainer1.Panel1.ResumeLayout(false);
      this.splitContainer1.Panel2.ResumeLayout(false);
      this.splitContainer1.ResumeLayout(false);
      this.splitContainer2.Panel1.ResumeLayout(false);
      this.splitContainer2.Panel2.ResumeLayout(false);
      this.splitContainer2.Panel2.PerformLayout();
      this.splitContainer2.ResumeLayout(false);
      this.splitContainer3.Panel1.ResumeLayout(false);
      this.splitContainer3.Panel2.ResumeLayout(false);
      this.splitContainer3.Panel2.PerformLayout();
      this.splitContainer3.ResumeLayout(false);
      this.ResumeLayout(false);
    }
  }
}
