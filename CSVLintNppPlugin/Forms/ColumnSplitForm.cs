﻿using CSVLint;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace CSVLintNppPlugin.Forms
{
    public partial class ColumnSplitForm : CSVLintNppPlugin.Forms.CsvEditFormBase
    {
        public ColumnSplitForm()
        {
            InitializeComponent();
        }
        public int SplitCode { get; set; }
        public int SplitColumn { get; set; }
        public string SplitParam { get; set; }
        public bool SplitRemove { get; set; }

        public void InitialiseSetting(CsvDefinition csvdef)
        {
            // add all columns to list
            for (var i = 0; i < csvdef.Fields.Count; i++)
            {
                var str = csvdef.Fields[i].Name;
                cmbSelectColumn.Items.Add(str);
            }

            // default select "(select column)" item
            cmbSelectColumn.SelectedIndex = 0;
        }
        private void cmbSelectColumn_SelectedIndexChanged(object sender, EventArgs e)
        {
            EvaluateOkButton();
        }

        private void OnRadioBtn_CheckedChanged(object sender, EventArgs e)
        {
            // which checkbox, see index in Tag property
            bool chk = (sender as RadioButton).Checked;
            ToggleControlBasedOnControl((sender as RadioButton), chk);

            EvaluateOkButton();
        }
        private void EvaluateOkButton()
        {
            // can not press OK when nothing selected
            btnOk.Enabled = (cmbSelectColumn.SelectedIndex > 0);
        }

        private void ColumnSplitForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // pass split parameters to previous form
            SplitCode = 0;
            if (rdbtnSplitValid.Checked) SplitCode = 1;
            if (rdbtnSplitCharacter.Checked) SplitCode = 2;
            if (rdbtnSplitSubstring.Checked) SplitCode = 3;

            // which column index
            SplitColumn = cmbSelectColumn.SelectedIndex - 1; // zero based, frist item is a dummy

            // extra parameters
            SplitParam = "";
            if (rdbtnSplitCharacter.Checked) SplitParam = txtSplitCharacter.Text;
            if (rdbtnSplitSubstring.Checked) SplitParam = txtSplitSubstring.Text;

            // remove original column
            SplitRemove = (chkDeleteOriginal.Checked);
        }
    }
}
