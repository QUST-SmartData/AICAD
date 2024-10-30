namespace AICAD
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.ai_split = new System.Windows.Forms.Button();
            this.auto_num = new System.Windows.Forms.Button();
            this.batch_replace_text = new System.Windows.Forms.Button();
            this.batch_print = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // ai_split
            // 
            this.ai_split.Location = new System.Drawing.Point(37, 23);
            this.ai_split.Name = "ai_split";
            this.ai_split.Size = new System.Drawing.Size(167, 77);
            this.ai_split.TabIndex = 0;
            this.ai_split.Text = "智能分图";
            this.ai_split.UseVisualStyleBackColor = true;
            this.ai_split.Click += new System.EventHandler(this.Split_CAD_Image);
            // 
            // auto_num
            // 
            this.auto_num.Location = new System.Drawing.Point(219, 23);
            this.auto_num.Name = "auto_num";
            this.auto_num.Size = new System.Drawing.Size(185, 77);
            this.auto_num.TabIndex = 1;
            this.auto_num.Text = "重新批量自动编号";
            this.auto_num.UseVisualStyleBackColor = true;
            this.auto_num.Click += new System.EventHandler(this.Auto_Numbering);
            // 
            // batch_replace_text
            // 
            this.batch_replace_text.Location = new System.Drawing.Point(37, 136);
            this.batch_replace_text.Name = "batch_replace_text";
            this.batch_replace_text.Size = new System.Drawing.Size(167, 77);
            this.batch_replace_text.TabIndex = 2;
            this.batch_replace_text.Text = "图框文本批量修改";
            this.batch_replace_text.UseVisualStyleBackColor = true;
            this.batch_replace_text.Click += new System.EventHandler(this.Text_Batch_Replacement);
            // 
            // batch_print
            // 
            this.batch_print.Location = new System.Drawing.Point(219, 136);
            this.batch_print.Name = "batch_print";
            this.batch_print.Size = new System.Drawing.Size(185, 77);
            this.batch_print.TabIndex = 3;
            this.batch_print.Text = "批量打印";
            this.batch_print.UseVisualStyleBackColor = true;
            this.batch_print.Click += new System.EventHandler(this.Batch_Print);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(436, 235);
            this.Controls.Add(this.batch_print);
            this.Controls.Add(this.batch_replace_text);
            this.Controls.Add(this.auto_num);
            this.Controls.Add(this.ai_split);
            this.Name = "Form1";
            this.Text = "智慧绘图工具";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button ai_split;
        private System.Windows.Forms.Button auto_num;
        private System.Windows.Forms.Button batch_replace_text;
        private System.Windows.Forms.Button batch_print;
    }
}