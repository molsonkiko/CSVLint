﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using CSVLint;
using CSVLintNppPlugin.CsvLint;
using CSVLintNppPlugin.Forms;
using Kbg.NppPluginNET.PluginInfrastructure;

namespace Kbg.NppPluginNET
{
    class Main
    {
        internal const string PluginName = "CSV Lint";
        public static Settings Settings = new Settings();
        public static CultureInfo dummyCulture;

        static string userConfigPath = null;
        static CsvLintWindow frmCsvLintDlg = null;
        static int idMyDlg = -1;

        // toolbar icons
        static Bitmap tbBmp = CSVLintNppPlugin.Properties.Resources.csvlint;
        static Icon tbIco = CSVLintNppPlugin.Properties.Resources.csvlint_black_32;
        static Icon tbIcoDM = CSVLintNppPlugin.Properties.Resources.csvlint_white_32;


        static Bitmap tbBmp_tbTab = CSVLintNppPlugin.Properties.Resources.csvlint;
        static IScintillaGateway editor = new ScintillaGateway(PluginBase.GetCurrentScintilla());
        static Icon tbIcon = null;

        static readonly Alpha sAlpha = editor.GetCaretLineBackAlpha();
        static readonly Colour sCaretLineColor = editor.GetCaretLineBack();
        internal static bool sShouldResetCaretBack = false;

        // Note: colors are stored as #RGB int values, R=most significant, B=least significant, for easy editing #RGB values (and also smaller storage space than strings)
        // however Kbg.NppPluginNET.PluginInfrastructure.Colour uses BGR int values, B=most significant, R=least significant
        public static readonly int COLORS_PER_SET = 13; // 1 default + 12 colors
        static readonly int[] DefaultColorsets = new int[] {
                0x000000, 0xFFFFFF, // normal colors
                0x000000, 0xC0EFFF,
                0x000000, 0xFFFFC0,
                0x000000, 0xFFE0FF,
                0x000000, 0xA0FFA0,
                0x000000, 0xFFC0E0,
                0x000000, 0xA0FFFF,
                0x000000, 0xFFE0C0,
                0x000000, 0xD0D0FF,
                0x000000, 0xCFFFA0,
                0x000000, 0xFFACFF,
                0x000000, 0x80FFBF,
                0x000000, 0xFFC0C0,
                0x000000, 0xFFFFFF, // normal colors (foreground)
                0x186CC0, 0xFFFFFF,
                0x909000, 0xFFFFFF,
                0x6C18C0, 0xFFFFFF,
                0x18C018, 0xFFFFFF,
                0xC0186C, 0xFFFFFF,
                0x00A0A0, 0xFFFFFF,
                0xC06C18, 0xFFFFFF,
                0x1818C0, 0xFFFFFF,
                0x6CC018, 0xFFFFFF,
                0xC018C0, 0xFFFFFF,
                0x10A060, 0xFFFFFF,
                0xC01818, 0xFFFFFF,
                0xDCDCCC, 0x3F3F3F, // dark mode (pastel)
                0xA0B8D0, 0x3F3F3F,
                0xD0D0A0, 0x3F3F3F,
                0xB8A0D0, 0x3F3F3F,
                0xA0D0A0, 0x3F3F3F,
                0xD0A0B8, 0x3F3F3F,
                0xA0D0D0, 0x3F3F3F,
                0xD0B8A0, 0x3F3F3F,
                0xA0A0D0, 0x3F3F3F,
                0xB8D0A0, 0x3F3F3F,
                0xD0A0D0, 0x3F3F3F,
                0xA0D0B8, 0x3F3F3F,
                0xD0A0A0, 0x3F3F3F,
                0xFFFFFF, 0x3F3F3F, // dark mode (neon)
                0x80BFFF, 0x002850,
                0xFFFF80, 0x505000,
                0xFFB0FF, 0x280050, 
                0x80FF80, 0x005000,
                0xFF90CF, 0x500028,
                0x80FFFF, 0x005050,
                0xFFBF80, 0x502800,
                0xC0C0FF, 0x000050,
                0xBFFF80, 0x285000,
                0xFF80FF, 0x500050,
                0x80FFBF, 0x005028,
                0xFF9090, 0x500000
            };

        // list of files and csv definition for each
        static Dictionary<string, CsvDefinition> FileCsvDef = new Dictionary<string, CsvDefinition>();
        static CsvDefinition _CurrnetCsvDef;

        public static void OnNotification(ScNotification notification)
        {
            uint code = notification.Header.Code;
            // This method is invoked whenever something is happening in notepad++
            // use eg. as
            // if (code == (uint)NppMsg.NPPN_xxx)
            // { ... }
            // or
            //
            // if (code == (uint)SciMsg.SCNxxx)
            // { ... }

            // changing tabs
            if ((code == (uint)NppMsg.NPPN_BUFFERACTIVATED) ||
                (code == (uint)NppMsg.NPPN_LANGCHANGED))
            {
                Main.CSVChangeFileTab();
            }

            // when closing a file
            if (code == (uint)NppMsg.NPPN_FILEBEFORECLOSE)
            {
                Main.RemoveCSVdef();
            }

            if (code > int.MaxValue) // windows messages
            {
                int wm = -(int)code;
                // leaving previous tab
                if (wm == 0x22A && sShouldResetCaretBack) // =554 WM_MDI_SETACTIVE
                {
                    // set caret line to default on file change
                    sShouldResetCaretBack = false;
                    var editor = new ScintillaGateway(PluginBase.GetCurrentScintilla());
                    editor.SetCaretLineBackAlpha(sAlpha);// Alpha.NOALPHA); // default
                    editor.SetCaretLineBack(sCaretLineColor);
                }
            }
        }

        internal static void CommandMenuInit()
        {
            // config folder
            StringBuilder sbIniFilePath = new StringBuilder(Win32.MAX_PATH);
            Win32.SendMessage(PluginBase.nppData._nppHandle, (uint)NppMsg.NPPM_GETPLUGINSCONFIGDIR, Win32.MAX_PATH, sbIniFilePath);
            userConfigPath = sbIniFilePath.ToString();
            if (!Directory.Exists(userConfigPath)) Directory.CreateDirectory(userConfigPath);

            // lexer xml file must exist
            TryCreateLexerXml(-1, false); // default: 0 = normal background, 2 = dark pastel

            // menu items
            //PluginBase.SetCommand(0, "MyMenuCommand", myMenuFunction, new ShortcutKey(false, false, false, Keys.None));
            PluginBase.SetCommand(0, "CSV Lint window", myDockableDialog); idMyDlg = 0;
            PluginBase.SetCommand(1, "---", null);
            PluginBase.SetCommand(2, "Analyse data report", AnalyseDataReport);
            PluginBase.SetCommand(3, "Count unique values", CountUniqueValues);
            PluginBase.SetCommand(4, "---", null);
            PluginBase.SetCommand(5, "Convert data", convertData);
            PluginBase.SetCommand(6, "Generate metadata", generateMetaData);
            PluginBase.SetCommand(7, "---", null);
            PluginBase.SetCommand(8, "&Settings", DoSettings);
            PluginBase.SetCommand(9, "&Documentation", DoDocumentation);
            PluginBase.SetCommand(10, "About", DoAboutForm);

            RefreshFromSettings();
        }

        internal static void RefreshFromSettings()
        {
            // the DateTime.TryParseExact requires a culture object, for much better performance DO NOT create on the fly for every call to EvaluateDateTime!
            var tmp = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            tmp.DateTimeFormat.Calendar.TwoDigitYearMax = Settings.intTwoDigitYearMax; // any cutoff you need
                                                        // incorrect: tmp.Calendar.TwoDigitYearMax = 2039
            dummyCulture = CultureInfo.ReadOnly(tmp);
        }

        internal static bool CheckConfigDarkMode()
        {
            string darkmodeenabled = "no";

            var xmlfile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Notepad++\\config.xml");
            try
            {
                XmlDocument config = new XmlDocument();
                config.Load(xmlfile);

                XmlNode darkmode = config.DocumentElement.SelectSingleNode("/NotepadPlus/GUIConfigs/GUIConfig[@name='DarkMode']");
                darkmodeenabled = (darkmode != null) &&
                    (darkmode as XmlElement).HasAttribute("enable") ? darkmode.Attributes["enable"].Value : "no";
            }
            catch { };

            return darkmodeenabled == "yes";
        }

        internal static void TryCreateLexerXml(int presetidx, bool overwrite)
        {
            var filename = Path.Combine(userConfigPath, "CSVLint.xml");
            // create language xml in plugin config directory if needed
            if ((!File.Exists(filename)) || overwrite)
            {
                // when initially creating CSVLintxml for first time
                if (presetidx == -1)
                {
                    // initially give users random color preset, i.e. distribute random among all users, to see which they like best
                    bool checkdarkmode = CheckConfigDarkMode();
                    var sec = DateTime.Now.Second % 2; // semi-random 0..1
                    presetidx = (checkdarkmode ? 2 : 0) + sec; // 0..1 for normal, 2..3 for dark mode
                }

                // create syntax color xml
                if (!CreateLexerXML(filename, presetidx))
                {
                    var errmsg = string.Format("Unable to create {0}.xml in folder {1}", PluginName, userConfigPath);
                    MessageBox.Show(errmsg, "Error saving CSVLint.xml", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        internal static bool CreateLexerXML(string filename, int presetidx)
        {
            string[] presets = new string[] { "normal mode (background colors)", "normal mode (foreground colors)", "dark mode (pastel)", "dark mode (neon)" };
            string[] tags = new string[] { "instre1", "instre2", "type1", "type2", "type3", "type4", "type5", "type6" };

            // Create an XmlWriterSettings object with the correct options.
            XmlWriterSettings xmlsettings = new XmlWriterSettings();
            xmlsettings.Indent = true;
            xmlsettings.IndentChars = "\t"; //  "\t";
            xmlsettings.OmitXmlDeclaration = false;
            //settings.Encoding = System.Text.Encoding.UTF8; // NOTE: this results in UTF-8-BOM, Notepad++ can only read UTF-8 xml
            xmlsettings.Encoding = new UTF8Encoding(false); // The false means, do not emit the BOM.

            try
            {
                using (XmlWriter writer = XmlWriter.Create(filename, xmlsettings))
                {

                    writer.WriteStartDocument();
                    writer.WriteStartElement("NotepadPlus");

                    // keywords
                    writer.WriteStartElement("Languages");
                    writer.WriteStartElement("Language");
                        writer.WriteAttributeString("name", "CSVLint");
                        writer.WriteAttributeString("ext", "csv");
                        writer.WriteAttributeString("commentLine", "#");
                        writer.WriteAttributeString("commentStart", "#[");
                        writer.WriteAttributeString("commentEnd", "]#");

                    for (int i = 0; i < tags.Length; i++)
                    {
                        writer.WriteStartElement("Keywords");
                        writer.WriteAttributeString("name", tags[i]);
                        writer.WriteEndElement();
                    }

                    writer.WriteEndElement();
                    writer.WriteEndElement();

                    // colors
                    writer.WriteStartElement("LexerStyles");
                    writer.WriteStartElement("LexerType");
                        writer.WriteAttributeString("name", "CSVLint");
                        writer.WriteAttributeString("desc", "CSV Linter and validator");
                        writer.WriteAttributeString("excluded", "no");

                    var coloridx = 0;
                    for (int ps = 0; ps < presets.Length; ps++)
                    {
                        // comment preset name
                        writer.WriteComment(presets[ps]);

                        if (presetidx != ps) writer.WriteRaw("\r\n<!--");

                        for (int i = 0; i < COLORS_PER_SET; i++)
                        {
                            //writer.WriteStartElement("WordsStyle");
                            //writer.WriteAttributeString("styleID", i.ToString());
                                //writer.WriteAttributeString("name", (i == 0 ? "Default" : "ColumnColor" + i.ToString()));
                                //writer.WriteAttributeString("fgColor", "000000");
                                //writer.WriteAttributeString("bgColor", colors[i]);
                                //writer.WriteAttributeString("fontName", "");
                                //writer.WriteAttributeString("fontStyle", "0");
                            //writer.WriteEndElement();
                            var name = i == 0 ? "Default" : "ColumnColor" + i.ToString();
                            var fgcolor = DefaultColorsets[coloridx].ToString("X6");
                            var bgcolor = DefaultColorsets[coloridx + 1].ToString("X6");

                            var bold = ps == 0 || i == 0 ? "0" : "1";
                            var str = string.Format("\r\n\t\t\t<WordsStyle styleID=\"{0}\" name=\"{1}\" fgColor=\"{2}\" bgColor=\"{3}\" fontName=\"\" fontStyle=\"{4}\" />", i, name, fgcolor, bgcolor, bold);
                            writer.WriteRaw(str);

                            coloridx += 2;
                        }

                        if (presetidx != ps) writer.WriteRaw("\r\n-->");
                        writer.WriteRaw("\r\n\t\t");
                    };

                    writer.WriteEndElement();

                    writer.Flush();
                    writer.Close();
                } // End Using writer
            }
            catch
            {
                return false;
            }

            return true;
        }

        // NOTE: calling StyleSetBack/StyleSetFore does not change the style permanently
        // i.e. when you switch tabs or open another file it reset to the original XML colors
        //internal static void SetLexerColors(int presetidx)
        //{
        //    var coloridx = 2 * presetidx * COLORS_PER_SET; // 2* = 2 colors per style item
        //
        //    for (int idx = 0; idx < COLORS_PER_SET; idx++)
        //    {
        //        // get foreground and background colors
        //        var rgb_fore = DefaultColorsets[coloridx];
        //        var rgb_back = DefaultColorsets[coloridx + 1];
        //
        //        // bold or not
        //        var bold = !(presetidx == 0 || idx == 0);
        //
        //        // update styles immediately
        //        // Note: convert RGB int values to indivisual R, G and B values
        //        editor.StyleSetBack(idx, new Colour( ((rgb_back >> 16) & 0xFF), ((rgb_back >> 8) & 0xFF), (rgb_back & 0xFF) ));
        //        editor.StyleSetFore(idx, new Colour( ((rgb_fore >> 16) & 0xFF), ((rgb_fore >> 8) & 0xFF), (rgb_fore & 0xFF) ));
        //        editor.StyleSetBold(idx, bold);
        //
        //        // next color
        //        coloridx += 2;
        //    }
        //}

        internal static void SetToolBarIcon()
        {
            // create struct
            toolbarIcons tbIcons = new toolbarIcons();

            // add bmp icon
            tbIcons.hToolbarBmp = tbBmp.GetHbitmap();
            tbIcons.hToolbarIcon = tbIco.Handle;            // icon with black lines
            tbIcons.hToolbarIconDarkMode = tbIcoDM.Handle;  // icon with light grey lines

            // convert to c++ pointer
            IntPtr pTbIcons = Marshal.AllocHGlobal(Marshal.SizeOf(tbIcons));
            Marshal.StructureToPtr(tbIcons, pTbIcons, false);

            // call Notepad++ api
            Win32.SendMessage(PluginBase.nppData._nppHandle, (uint)NppMsg.NPPM_ADDTOOLBARICON_FORDARKMODE, PluginBase._funcItems.Items[idMyDlg]._cmdID, pTbIcons);

            // release pointer
            Marshal.FreeHGlobal(pTbIcons);
        }

        public static void CSVChangeFileTab()
        {
            // Notepad++ switched to a different file tab
            INotepadPPGateway notepad = new NotepadPPGateway();
            string filename = notepad.GetCurrentFilePath();

            CsvDefinition csvdef;

            // check if already in list
            if (!FileCsvDef.TryGetValue(filename, out csvdef))
            {
                // read schema.ini file
                var lines = CsvSchemaIni.ReadIniSection(filename);
                if (lines.Count > 0)
                {
                    // metadata from previously saved schema.ini
                    csvdef = new CsvDefinition(lines);
                }
                else
                {
                    // analyze and determine csv definition
                    csvdef = CsvAnalyze.InferFromData(true, '\0', false); // parameters '', false don't matter
                }
                FileCsvDef.Add(filename, csvdef);
            }

            // pass separator to lexer
            string sepchar = csvdef.Separator.ToString();
            string sepcol = Settings.SeparatorColor ? "1" : "0";
            editor.SetProperty("separator", sepchar);
            editor.SetProperty("separatorcolor", sepcol);

            // if fixed width
            if ((csvdef.Separator == '\0') && (csvdef.FieldWidths != null))
            {
                // also pass column widths to lexer
                var strwidths = "";
                for (var i = 0; i < csvdef.FieldWidths.Count; i++)
                {
                    strwidths += (i > 0 ? "," : "") + csvdef.FieldWidths[i].ToString();
                }

                editor.SetProperty("fixedwidths", strwidths);
            }
            editor.SetIdleStyling(IdleStyling.ALL);

            // keep current csvdef
            _CurrnetCsvDef = FileCsvDef[filename];

            if (frmCsvLintDlg != null)
            {
                frmCsvLintDlg.SetCsvDefinition(csvdef, false);
            }
        }

        public static void UpdateCSVChanges(CsvDefinition csvdef, bool saveini)
        {
            // Notepad++ switc to a different file tab
            INotepadPPGateway notepad = new NotepadPPGateway();
            string filename = notepad.GetCurrentFilePath();

            // overwrite old or add new csv definition
            FileCsvDef[filename] = csvdef;

            // pass separator to lexer
            string sepchar = csvdef.Separator.ToString();
            string sepcol = Settings.SeparatorColor ? "1" : "0";
            editor.SetProperty("separator", sepchar);
            editor.SetProperty("separatorcolor", sepcol);

            // if fixed width
            if ((csvdef.Separator == '\0') && (csvdef.FieldWidths != null))
            {
                var strwidths = "";
                for (var i = 0; i < csvdef.FieldWidths.Count; i++)
                {
                    strwidths += (i > 0 ? "," : "") + csvdef.FieldWidths[i].ToString();
                }

                editor.SetProperty("fixedwidths", strwidths);
            }
            editor.SetIdleStyling(IdleStyling.ALL);

            // also write to schema.ini file
            if (saveini)
            {
                if (!CsvSchemaIni.WriteIniSection(filename, csvdef.GetIniLines(), out string errmsg))
                {
                    MessageBox.Show(errmsg, "Error saving schema.ini", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        public static CsvDefinition GetCurrentCsvDef()
        {
            // Notepad++ switc to a different file tab
            INotepadPPGateway notepad = new NotepadPPGateway();
            string filename = notepad.GetCurrentFilePath();

            return FileCsvDef.TryGetValue(filename, out CsvDefinition result) ? result : null;
        }

        public static void RemoveCSVdef()
        {
            // Notepad++ closes a file, also remove the definition from list
            INotepadPPGateway notepad = new NotepadPPGateway();
            string filename = notepad.GetCurrentFilePath();

            // remove csv definition if existant
            FileCsvDef.Remove(filename);
        }

        internal static void PluginCleanUp()
        {
            // any clean up code here
        }

        internal static void DoSettings()
        {
            Settings.ShowDialog();
            RefreshFromSettings();
        }
        internal static void DoDocumentation()
        {
            // Call the Process.Start method to open the default browser with a URL:
            System.Diagnostics.Process.Start("https://github.com/BdR76/CSVLint/tree/master/docs#csv-lint-plug-in-documentation");
        }

        internal static void DoAboutForm()
        {
            using (var about = new AboutForm())
                about.ShowDialog();
        }

        internal static void convertData()
        {
            // get dictionary
            CsvDefinition csvdef = GetCurrentCsvDef();

            // show split column dialog
            var frmparam = new DataConvertForm();
            frmparam.InitialiseSetting();

            DialogResult r = frmparam.ShowDialog();

            // clear up
            frmparam.Dispose();

            // return true (OK) or false (Cancel)
            if (r == DialogResult.OK)
            {
                switch (Main.Settings.DataConvertType)
                {
                    case 1: // XML
                        CsvEdit.ConvertToXML(csvdef);
                        break;
                    case 2: // JSON
                        CsvEdit.ConvertToJSON(csvdef);
                        break;
                    default: // case 0: SQL
                        CsvEdit.ConvertToSQL(csvdef);
                        break;
                }
            }
        }

        internal static void generateMetaData()
        {
            // get dictionary
            CsvDefinition csvdef = GetCurrentCsvDef();
        
            // show metadata options
            var frmparam = new MetaDataGenerateForm();
            frmparam.InitialiseSetting();

            DialogResult r = frmparam.ShowDialog();
        
            // clear up
            frmparam.Dispose();
        
            // return true (OK) or false (Cancel)
            if (r == DialogResult.OK)
            {
                switch (Main.Settings.MetadataType)
                {
                    case 1: // schema JSON
                        CsvGenerateCode.GenerateSchemaJSON(csvdef); 
                        break;
                    case 2: // Python
                        CsvGenerateCode.GeneratePythonPanda(csvdef);
                        break;
                    case 3: // R - script
                        CsvGenerateCode.GenerateRScript(csvdef); 
                        break;
                    default: // case 0: schema ini
                        CsvGenerateCode.GenerateSchemaIni(csvdef); 
                        break;
                }
            }
        }

        internal static void AnalyseDataReport()
        {
            // get dictionary
            CsvDefinition csvdef = GetCurrentCsvDef();
            
            // check if valid dictionary
            if (csvdef.Fields.Count > 0)
            {
                // validate data
                CsvAnalyze.StatisticalReportData(csvdef);
            }
        }

        internal static void CountUniqueValues()
        {
            // get dictionary
            CsvDefinition csvdef = GetCurrentCsvDef();

            // check if valid dictionary
            if (csvdef.Fields.Count > 0)
            {
                // show unique values parameters form
                var frmunq = new UniqueValuesForm();
                frmunq.InitialiseSetting(csvdef);
                DialogResult r = frmunq.ShowDialog();

                // user clicked OK or Cancel
                List<int> colidx = new List<int>(frmunq.columnIndexes);
                bool sortBy = frmunq.sortBy;
                bool sortValue = frmunq.sortValue;
                bool sortDesc = frmunq.sortDesc;

                // clear up
                frmunq.Dispose();

                // return true (OK) or false (Cancel)
                if (r == DialogResult.OK)
                {
                    // count unique values
                    CsvAnalyze.CountUniqueValues(csvdef, colidx, sortBy, sortValue, sortDesc);
                }
            }
        }

        internal static void myDockableDialog()
        {
            if (frmCsvLintDlg == null)
            {
                // show dialog for first time
                frmCsvLintDlg = new CsvLintWindow();

                // icon
                using (Bitmap newBmp = new Bitmap(16, 16))
                {
                    Graphics g = Graphics.FromImage(newBmp);
                    ColorMap[] colorMap = new ColorMap[1];
                    colorMap[0] = new ColorMap();
                    colorMap[0].OldColor = Color.Fuchsia;
                    colorMap[0].NewColor = Color.FromKnownColor(KnownColor.ButtonFace);
                    ImageAttributes attr = new ImageAttributes();
                    attr.SetRemapTable(colorMap);
                    g.DrawImage(tbBmp_tbTab, new Rectangle(0, 0, 16, 16), 0, 0, 16, 16, GraphicsUnit.Pixel, attr);
                    tbIcon = Icon.FromHandle(newBmp.GetHicon());
                }

                // dockable window struct data
                NppTbData _nppTbData = new NppTbData();
                _nppTbData.hClient = frmCsvLintDlg.Handle;
                _nppTbData.pszName = "CSV Lint";
                _nppTbData.dlgID = idMyDlg;
                _nppTbData.uMask = NppTbMsg.DWS_DF_CONT_BOTTOM | NppTbMsg.DWS_ICONTAB | NppTbMsg.DWS_ICONBAR;
                _nppTbData.hIconTab = (uint)tbIcon.Handle;
                _nppTbData.pszModuleName = PluginName;
                IntPtr _ptrNppTbData = Marshal.AllocHGlobal(Marshal.SizeOf(_nppTbData));
                Marshal.StructureToPtr(_nppTbData, _ptrNppTbData, false);

                // register as dockable window
                Win32.SendMessage(PluginBase.nppData._nppHandle, (uint) NppMsg.NPPM_DMMREGASDCKDLG, 0, _ptrNppTbData);
            }
            else
            {
                // toggle on/off
                Win32.SendMessage(PluginBase.nppData._nppHandle,
                    (uint)(frmCsvLintDlg.Visible ? NppMsg.NPPM_DMMHIDE : NppMsg.NPPM_DMMSHOW),
                    0, frmCsvLintDlg.Handle);
            }

            // immediately show currnet csv metadata when activated
            CSVChangeFileTab();
        }

        public static string GetVersion()
        {
            // version for example "1.3.0.0"
            string ver = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            // if 4 version digits, remove last two ".0" if any, example  "1.3.0.0" ->  "1.3" or  "2.0.0.0" ->  "2.0"
            while ((ver.Length > 4) && (ver.Substring(ver.Length - 2, 2) == ".0"))
            {
                ver = ver.Substring(0, ver.Length - 2);
            }
            return ver;
        }
    }
}