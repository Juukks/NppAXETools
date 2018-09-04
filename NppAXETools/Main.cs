using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using Kbg.NppPluginNET.PluginInfrastructure;

namespace Kbg.NppPluginNET
{
    class Main
    {
        internal const string PluginName = "AXE Tools";
        static string iniFilePath = null;
        static bool someSetting = false;
        static Bitmap tbBmp = Properties.Resources.star;
        static Bitmap tbBmp_tbTab = Properties.Resources.star_bmp;

        public static void OnNotification(ScNotification notification)
        {  
            // This method is invoked whenever something is happening in notepad++
            // use eg. as
            // if (notification.Header.Code == (uint)NppMsg.NPPN_xxx)
            // { ... }
            // or
            //
            // if (notification.Header.Code == (uint)SciMsg.SCNxxx)
            // { ... }
        }

        internal static void CommandMenuInit()
        {
            StringBuilder sbIniFilePath = new StringBuilder(Win32.MAX_PATH);
            Win32.SendMessage(PluginBase.nppData._nppHandle, (uint) NppMsg.NPPM_GETPLUGINSCONFIGDIR, Win32.MAX_PATH, sbIniFilePath);
            iniFilePath = sbIniFilePath.ToString();
            if (!Directory.Exists(iniFilePath)) Directory.CreateDirectory(iniFilePath);
            iniFilePath = Path.Combine(iniFilePath, PluginName + ".ini");
            someSetting = (Win32.GetPrivateProfileInt("SomeSection", "SomeKey", 0, iniFilePath) != 0);

            PluginBase.SetCommand(0, "C7GSP converter", c7gspFunction, new ShortcutKey(false, false, false, Keys.None));
        }

        internal static void SetToolBarIcon()
        {
            toolbarIcons tbIcons = new toolbarIcons();
            tbIcons.hToolbarBmp = tbBmp.GetHbitmap();
            IntPtr pTbIcons = Marshal.AllocHGlobal(Marshal.SizeOf(tbIcons));
            Marshal.StructureToPtr(tbIcons, pTbIcons, false);
            Marshal.FreeHGlobal(pTbIcons);
        }

        internal static void PluginCleanUp()
        {
            Win32.WritePrivateProfileString("SomeSection", "SomeKey", someSetting ? "1" : "0", iniFilePath);
        }


        internal static void c7gspFunction()
        {
            
            String selStr;
            var scintilla = new ScintillaGateway(PluginBase.GetCurrentScintilla());

            //Is there any text selected
            if (scintilla.GetSelText() != "") {

                //Calculating selections first line begin
                Position selStartPos = scintilla.GetSelectionStart();
                int startLineNum = scintilla.LineFromPosition(selStartPos);
                Position startLinePos = scintilla.PositionFromLine(startLineNum);

                //Calculating selections last line end
                Position selEndPos = scintilla.GetSelectionEnd();
                int endLineNum = scintilla.LineFromPosition(selEndPos);
                Position endLinePos = scintilla.GetLineEndPosition(endLineNum);

                //Setting the selection as needed
                scintilla.SetSel(startLinePos, endLinePos);

                //Picking up the selected text to memory for parsing
                selStr = scintilla.GetSelText();

                //Iterating through the lines
                using (StringReader reader = new StringReader(selStr.ToString())) {
                    string dstr = "";
                    string line;
                    int ignoreMe;

                    bool parsingGT = false;

                    int tt = 0, np = 0, na = 0, gtrc = 0;
                    string ns = "";
                    string modifiers = "";

                    while ((line = reader.ReadLine()) != null) {
                        //Parse only lines which not empty
                        if (line.Length > 0)
                        {
                            //If parsing is ongoing
                            if (parsingGT == true)
                            {
                                //If we are not in header line and not in the line which begins the analyse
                                if (line.Substring(9, 3) != "MTT" && !(int.TryParse(line.Substring(0, 3), out ignoreMe)))
                                {
                                    //Determining which variables the line will contain
                                    if (line.Length == 12) {
                                        modifiers += "MTT=" + line.Substring(9, 3).Replace(" ", string.Empty);
                                    }
                                    else if(line.Length == 17)
                                    {
                                        if(!string.IsNullOrWhiteSpace(line.Substring(9, 3))) { 
                                            modifiers += "MTT=" + line.Substring(9, 3).Replace(" ", string.Empty) + ",";
                                        }

                                        modifiers += "MNP=" + line.Substring(14, 3).Replace(" ", string.Empty);
                                    }
                                    else if (line.Length == 22)
                                    {
                                        if (!string.IsNullOrWhiteSpace(line.Substring(9, 3)))
                                        {
                                            modifiers += "MTT=" + line.Substring(9, 3).Replace(" ", string.Empty) + ",";
                                        }
                                        if (!string.IsNullOrWhiteSpace(line.Substring(14, 3)))
                                        {
                                            modifiers += "MNP=" + line.Substring(14, 3).Replace(" ", string.Empty) + ",";
                                        }

                                        modifiers += "MNA=" + line.Substring(19, 3).Replace(" ", string.Empty);
                                    }
                                    else
                                    {
                                        if (!string.IsNullOrWhiteSpace(line.Substring(9, 3)))
                                        {
                                            modifiers += "MTT=" + line.Substring(9, 3).Replace(" ", string.Empty) + ",";
                                        }
                                        if (!string.IsNullOrWhiteSpace(line.Substring(14, 3)))
                                        {
                                            modifiers += "MNP=" + line.Substring(14, 3).Replace(" ", string.Empty) + ",";
                                        }
                                        if (!string.IsNullOrWhiteSpace(line.Substring(19, 3)))
                                        {
                                            modifiers += "MNA=" + line.Substring(19, 3).Replace(" ", string.Empty) + ",";
                                        }

                                        modifiers += "MNS=" + line.Substring(24, (line.Length - 24)).Replace(" ", string.Empty);
                                    }

                                }
                            }

                            //Start parsing when reached the line which begins with number
                            if (int.TryParse(line.Substring(0, 3), out ignoreMe))
                            {
                                //Are we still parsing
                                if (parsingGT == true)
                                {
                                    //Printing previous definition and resetting values
                                    dstr += "C7GSI:TT=" + tt + ",NP=" + np + ",NA=" + na + ",NS=" + ns + ",GTRC=" + gtrc + ";" + "\n";
                                    tt = int.Parse(line.Substring(0, 3));
                                    np = int.Parse(line.Substring(5, 2));
                                    na = int.Parse(line.Substring(9, 3));
                                    ns = line.Substring(14, 16).Replace(" ", string.Empty);
                                    gtrc = na = int.Parse(line.Substring(56, 3));
                                }
                                else
                                {
                                    //Gathering information from the line
                                    tt = int.Parse(line.Substring(0, 3));
                                    np = int.Parse(line.Substring(5, 2));
                                    na = int.Parse(line.Substring(9, 3));
                                    ns = line.Substring(14, 16).Replace(" ", string.Empty);
                                    gtrc = int.Parse(line.Substring(56, 3));

                                    parsingGT = true;
                                }
                            }

                        }
                        else {
                            //Printing previous definition and resetting values
                            dstr += "C7GSI:TT=" + tt + ",NP=" + np + ",NA=" + na + ",NS=" + ns + ",GTRC=" + gtrc + ";" + "\n";
                            //Is there any modifiers to print
                            if (modifiers != "")
                            {
                                dstr += "C7GCC:TT=" + tt + ",NP=" + np + ",NA=" + na + ",NS=" + ns + "," + modifiers + ";" + "\n";
                            }

                            //Resetting variables
                            tt = 0;
                            np = 0;
                            na = 0;
                            gtrc = 0;
                            ns = "0";
                            modifiers = "";

                            parsingGT = false;
                        }
                    }

                    dstr += "C7GSI:TT=" + tt + ",NP=" + np + ",NA=" + na + ",NS=" + ns + ",GTRC=" + gtrc + ";" + "\n";
                    //Is there any modifiers to print
                    if (modifiers != "")
                    {
                        dstr += "C7GCC:TT=" + tt + ",NP=" + np + ",NA=" + na + ",NS=" + ns + "," + modifiers + ";" + "\n";
                    }

                    MessageBox.Show(dstr);
                }
                
            }

            
        }

        
    }
}
