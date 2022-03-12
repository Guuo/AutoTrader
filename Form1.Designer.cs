using System.Runtime.InteropServices;

namespace AutoTrader
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.GetDataBtn = new System.Windows.Forms.Button();
            this.CalculateButton = new System.Windows.Forms.Button();
            this.Backtest = new System.Windows.Forms.Button();
            this.StrategySelectionBox = new System.Windows.Forms.ListBox();
            this.DateTimePickerFrom = new System.Windows.Forms.DateTimePicker();
            this.DateTimePickerTo = new System.Windows.Forms.DateTimePicker();
            this.GetDataCustomTimeRange = new System.Windows.Forms.Button();
            this.SaveToDiskCheckBox = new System.Windows.Forms.CheckBox();
            this.FileNameTextBox = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.IntervalComboBox = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.DateTimePickerFromTime = new System.Windows.Forms.DateTimePicker();
            this.DateTimePickerToTime = new System.Windows.Forms.DateTimePicker();
            this.LoadDataFromMemory = new System.Windows.Forms.CheckBox();
            this.WMAControl = new System.Windows.Forms.NumericUpDown();
            this.ShortRControl = new System.Windows.Forms.NumericUpDown();
            this.LongRControl = new System.Windows.Forms.NumericUpDown();
            this.LinRegControl = new System.Windows.Forms.NumericUpDown();
            this.liveMonitorToggle = new System.Windows.Forms.CheckBox();
            this.SymbolIDTextBox = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.WebSocketTest = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.WMAControl)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ShortRControl)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.LongRControl)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.LinRegControl)).BeginInit();
            this.SuspendLayout();
            // 
            // GetDataBtn
            // 
            this.GetDataBtn.Location = new System.Drawing.Point(35, 33);
            this.GetDataBtn.Name = "GetDataBtn";
            this.GetDataBtn.Size = new System.Drawing.Size(104, 27);
            this.GetDataBtn.TabIndex = 0;
            this.GetDataBtn.Text = "getdata";
            this.GetDataBtn.UseVisualStyleBackColor = true;
            this.GetDataBtn.Click += new System.EventHandler(this.GetDataBtn_Click);
            // 
            // CalculateButton
            // 
            this.CalculateButton.Location = new System.Drawing.Point(281, 33);
            this.CalculateButton.Name = "CalculateButton";
            this.CalculateButton.Size = new System.Drawing.Size(115, 27);
            this.CalculateButton.TabIndex = 1;
            this.CalculateButton.Text = "CalculateButton";
            this.CalculateButton.UseVisualStyleBackColor = true;
            this.CalculateButton.Click += new System.EventHandler(this.CalculateButton_Click);
            // 
            // Backtest
            // 
            this.Backtest.Location = new System.Drawing.Point(35, 130);
            this.Backtest.Name = "Backtest";
            this.Backtest.Size = new System.Drawing.Size(75, 23);
            this.Backtest.TabIndex = 2;
            this.Backtest.Text = "Backtest";
            this.Backtest.UseVisualStyleBackColor = true;
            this.Backtest.Click += new System.EventHandler(this.Backtest_Click_1);
            // 
            // StrategySelectionBox
            // 
            this.StrategySelectionBox.FormattingEnabled = true;
            this.StrategySelectionBox.ItemHeight = 15;
            this.StrategySelectionBox.Location = new System.Drawing.Point(12, 344);
            this.StrategySelectionBox.Name = "StrategySelectionBox";
            this.StrategySelectionBox.Size = new System.Drawing.Size(225, 94);
            this.StrategySelectionBox.TabIndex = 3;
            // 
            // DateTimePickerFrom
            // 
            this.DateTimePickerFrom.Location = new System.Drawing.Point(281, 175);
            this.DateTimePickerFrom.Name = "DateTimePickerFrom";
            this.DateTimePickerFrom.Size = new System.Drawing.Size(200, 23);
            this.DateTimePickerFrom.TabIndex = 4;
            // 
            // DateTimePickerTo
            // 
            this.DateTimePickerTo.Location = new System.Drawing.Point(281, 216);
            this.DateTimePickerTo.Name = "DateTimePickerTo";
            this.DateTimePickerTo.Size = new System.Drawing.Size(200, 23);
            this.DateTimePickerTo.TabIndex = 5;
            // 
            // GetDataCustomTimeRange
            // 
            this.GetDataCustomTimeRange.Enabled = false;
            this.GetDataCustomTimeRange.Location = new System.Drawing.Point(597, 186);
            this.GetDataCustomTimeRange.Name = "GetDataCustomTimeRange";
            this.GetDataCustomTimeRange.Size = new System.Drawing.Size(79, 42);
            this.GetDataCustomTimeRange.TabIndex = 6;
            this.GetDataCustomTimeRange.Text = "Get data between";
            this.GetDataCustomTimeRange.UseVisualStyleBackColor = true;
            this.GetDataCustomTimeRange.Click += new System.EventHandler(this.GetDataCustomTimeRange_Click);
            // 
            // SaveToDiskCheckBox
            // 
            this.SaveToDiskCheckBox.AutoSize = true;
            this.SaveToDiskCheckBox.Location = new System.Drawing.Point(683, 200);
            this.SaveToDiskCheckBox.Name = "SaveToDiskCheckBox";
            this.SaveToDiskCheckBox.Size = new System.Drawing.Size(114, 19);
            this.SaveToDiskCheckBox.TabIndex = 7;
            this.SaveToDiskCheckBox.Text = "Save data to disk";
            this.SaveToDiskCheckBox.UseVisualStyleBackColor = true;
            // 
            // FileNameTextBox
            // 
            this.FileNameTextBox.Location = new System.Drawing.Point(683, 234);
            this.FileNameTextBox.Name = "FileNameTextBox";
            this.FileNameTextBox.Size = new System.Drawing.Size(114, 23);
            this.FileNameTextBox.TabIndex = 8;
            this.FileNameTextBox.Text = "1Y_1HData.csv";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(730, 216);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(18, 15);
            this.label1.TabIndex = 9;
            this.label1.Text = "as";
            // 
            // IntervalComboBox
            // 
            this.IntervalComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.IntervalComboBox.Enabled = false;
            this.IntervalComboBox.FormattingEnabled = true;
            this.IntervalComboBox.Items.AddRange(new object[] {
            "1",
            "5",
            "15",
            "30",
            "45",
            "60",
            "120",
            "240",
            "300"});
            this.IntervalComboBox.Location = new System.Drawing.Point(683, 263);
            this.IntervalComboBox.Name = "IntervalComboBox";
            this.IntervalComboBox.Size = new System.Drawing.Size(44, 23);
            this.IntervalComboBox.TabIndex = 10;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(532, 266);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(145, 15);
            this.label2.TabIndex = 11;
            this.label2.Text = "Candle interval in minutes";
            // 
            // DateTimePickerFromTime
            // 
            this.DateTimePickerFromTime.Format = System.Windows.Forms.DateTimePickerFormat.Time;
            this.DateTimePickerFromTime.Location = new System.Drawing.Point(487, 175);
            this.DateTimePickerFromTime.Name = "DateTimePickerFromTime";
            this.DateTimePickerFromTime.ShowUpDown = true;
            this.DateTimePickerFromTime.Size = new System.Drawing.Size(64, 23);
            this.DateTimePickerFromTime.TabIndex = 12;
            this.DateTimePickerFromTime.Value = new System.DateTime(2021, 4, 23, 0, 0, 0, 0);
            // 
            // DateTimePickerToTime
            // 
            this.DateTimePickerToTime.Format = System.Windows.Forms.DateTimePickerFormat.Time;
            this.DateTimePickerToTime.Location = new System.Drawing.Point(487, 216);
            this.DateTimePickerToTime.Name = "DateTimePickerToTime";
            this.DateTimePickerToTime.ShowUpDown = true;
            this.DateTimePickerToTime.Size = new System.Drawing.Size(64, 23);
            this.DateTimePickerToTime.TabIndex = 13;
            this.DateTimePickerToTime.Value = new System.DateTime(2021, 4, 23, 0, 0, 0, 0);
            // 
            // LoadDataFromMemory
            // 
            this.LoadDataFromMemory.AutoSize = true;
            this.LoadDataFromMemory.Checked = true;
            this.LoadDataFromMemory.CheckState = System.Windows.Forms.CheckState.Checked;
            this.LoadDataFromMemory.Location = new System.Drawing.Point(402, 38);
            this.LoadDataFromMemory.Name = "LoadDataFromMemory";
            this.LoadDataFromMemory.Size = new System.Drawing.Size(192, 19);
            this.LoadDataFromMemory.TabIndex = 14;
            this.LoadDataFromMemory.Text = "Use latest price data in memory";
            this.LoadDataFromMemory.UseVisualStyleBackColor = true;
            // 
            // WMAControl
            // 
            this.WMAControl.Location = new System.Drawing.Point(56, 174);
            this.WMAControl.Name = "WMAControl";
            this.WMAControl.Size = new System.Drawing.Size(35, 23);
            this.WMAControl.TabIndex = 15;
            // 
            // ShortRControl
            // 
            this.ShortRControl.Location = new System.Drawing.Point(138, 175);
            this.ShortRControl.Name = "ShortRControl";
            this.ShortRControl.Size = new System.Drawing.Size(35, 23);
            this.ShortRControl.TabIndex = 16;
            // 
            // LongRControl
            // 
            this.LongRControl.Location = new System.Drawing.Point(97, 175);
            this.LongRControl.Name = "LongRControl";
            this.LongRControl.Size = new System.Drawing.Size(35, 23);
            this.LongRControl.TabIndex = 17;
            // 
            // LinRegControl
            // 
            this.LinRegControl.Location = new System.Drawing.Point(15, 174);
            this.LinRegControl.Name = "LinRegControl";
            this.LinRegControl.Size = new System.Drawing.Size(35, 23);
            this.LinRegControl.TabIndex = 18;
            // 
            // liveMonitorToggle
            // 
            this.liveMonitorToggle.AutoSize = true;
            this.liveMonitorToggle.Location = new System.Drawing.Point(678, 388);
            this.liveMonitorToggle.Name = "liveMonitorToggle";
            this.liveMonitorToggle.Size = new System.Drawing.Size(110, 19);
            this.liveMonitorToggle.TabIndex = 19;
            this.liveMonitorToggle.Text = "Live Monitoring";
            this.liveMonitorToggle.UseVisualStyleBackColor = true;
            this.liveMonitorToggle.CheckedChanged += new System.EventHandler(this.liveMonitorToggle_CheckedChanged);
            // 
            // SymbolIDTextBox
            // 
            this.SymbolIDTextBox.Location = new System.Drawing.Point(683, 292);
            this.SymbolIDTextBox.Name = "SymbolIDTextBox";
            this.SymbolIDTextBox.Size = new System.Drawing.Size(100, 23);
            this.SymbolIDTextBox.TabIndex = 20;
            this.SymbolIDTextBox.Text = "8874";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(615, 295);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(61, 15);
            this.label3.TabIndex = 21;
            this.label3.Text = "Symbol ID";
            // 
            // WebSocketTest
            // 
            this.WebSocketTest.Location = new System.Drawing.Point(373, 366);
            this.WebSocketTest.Name = "WebSocketTest";
            this.WebSocketTest.Size = new System.Drawing.Size(75, 23);
            this.WebSocketTest.TabIndex = 22;
            this.WebSocketTest.Text = "button1";
            this.WebSocketTest.UseVisualStyleBackColor = true;
            this.WebSocketTest.Click += new System.EventHandler(this.WebSocketTest_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.WebSocketTest);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.SymbolIDTextBox);
            this.Controls.Add(this.liveMonitorToggle);
            this.Controls.Add(this.LinRegControl);
            this.Controls.Add(this.LongRControl);
            this.Controls.Add(this.ShortRControl);
            this.Controls.Add(this.WMAControl);
            this.Controls.Add(this.LoadDataFromMemory);
            this.Controls.Add(this.DateTimePickerToTime);
            this.Controls.Add(this.DateTimePickerFromTime);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.IntervalComboBox);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.FileNameTextBox);
            this.Controls.Add(this.SaveToDiskCheckBox);
            this.Controls.Add(this.GetDataCustomTimeRange);
            this.Controls.Add(this.DateTimePickerTo);
            this.Controls.Add(this.DateTimePickerFrom);
            this.Controls.Add(this.StrategySelectionBox);
            this.Controls.Add(this.Backtest);
            this.Controls.Add(this.CalculateButton);
            this.Controls.Add(this.GetDataBtn);
            this.Name = "Form1";
            this.Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)(this.WMAControl)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ShortRControl)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.LongRControl)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.LinRegControl)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button GetDataBtn;
        private System.Windows.Forms.Button CalculateButton;
        private System.Windows.Forms.Button Backtest;
        private System.Windows.Forms.ListBox StrategySelectionBox;
        private System.Windows.Forms.DateTimePicker DateTimePickerFrom;
        private System.Windows.Forms.DateTimePicker DateTimePickerTo;
        private System.Windows.Forms.Button GetDataCustomTimeRange;
        private System.Windows.Forms.CheckBox SaveToDiskCheckBox;
        private System.Windows.Forms.TextBox FileNameTextBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox IntervalComboBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.DateTimePicker DateTimePickerFromTime;
        private System.Windows.Forms.DateTimePicker DateTimePickerToTime;
        private System.Windows.Forms.CheckBox LoadDataFromMemory;
        private System.Windows.Forms.NumericUpDown WMAControl;
        private System.Windows.Forms.NumericUpDown ShortRControl;
        private System.Windows.Forms.NumericUpDown LongRControl;
        private System.Windows.Forms.NumericUpDown LinRegControl;
        private System.Windows.Forms.CheckBox liveMonitorToggle;
        private System.Windows.Forms.TextBox SymbolIDTextBox;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button WebSocketTest;
    }
}

