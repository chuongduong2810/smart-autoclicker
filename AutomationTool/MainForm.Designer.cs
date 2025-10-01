using Microsoft.AspNetCore.Components.WebView.WindowsForms;

namespace AutomationTool;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;
    private BlazorWebView blazorWebView1;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.blazorWebView1 = new BlazorWebView();
        this.SuspendLayout();
        
        // 
        // blazorWebView1
        // 
        this.blazorWebView1.Dock = DockStyle.Fill;
        this.blazorWebView1.Location = new Point(0, 0);
        this.blazorWebView1.Name = "blazorWebView1";
        this.blazorWebView1.Size = new Size(1200, 800);
        this.blazorWebView1.TabIndex = 0;
        
        // 
        // MainForm
        // 
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(1200, 800);
        this.Controls.Add(this.blazorWebView1);
        this.Name = "MainForm";
        this.Text = "Smart Auto Clicker";
        this.ResumeLayout(false);
    }
}


