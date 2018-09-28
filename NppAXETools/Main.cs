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

            PluginBase.SetCommand(0, "C7GSP converter", C7GSPFunction, new ShortcutKey(false, false, false, Keys.None));
        }


        internal static void PluginCleanUp()
        {
            Win32.WritePrivateProfileString("SomeSection", "SomeKey", someSetting ? "1" : "0", iniFilePath);
        }


        internal static void C7GSPFunction()
        {
            //Preparing some variables
            var scintilla = new ScintillaGateway(PluginBase.GetCurrentScintilla());

            //Starting Undo "recording". Next steps can be undo with one undo command
            scintilla.BeginUndoAction();

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

                //Preparing needed variables
                int ignoreMe = 0;

                //Gathered information
                string line = "";
                int lineFeedLen = 0;
                int tt = 0, np = 0, na = 0, gtrc = 0;
                string ns = "";
                string modifiers = "";

                //Loopping through the selected lines
                int i = startLineNum;
                while ( i <= endLineNum)
                {
                    //Line to the memory
                    line = scintilla.GetLine(i);
                    
                    if(line.Length > 2)
                    {
                        //Checking did we get a fresh GT line (three first chars are int (TT))
                        if (int.TryParse(line.Substring(0, 3), out ignoreMe))
                        {
                            //Gathering the basic GT information
                            tt = int.Parse(line.Substring(0, 3));
                            np = int.Parse(line.Substring(5, 2));
                            na = int.Parse(line.Substring(9, 3));
                            ns = line.Substring(14, 16).Replace(" ", string.Empty);
                            gtrc = na = int.Parse(line.Substring(56, 3));

                            //Move carret to the begin of the line
                            scintilla.SetCurrentPos(scintilla.PositionFromLine(i));
                            //Delete all from the line
                            scintilla.DelLineRight();
                            //Add text
                            scintilla.InsertText(scintilla.PositionFromLine(i), "C7GSI:TT=" + tt + ",NP=" + np + ",NA=" + ",NS=" + ns + ",GTRC=" + gtrc + ";");

                            //Checking next line if it it's not empty
                            if (scintilla.GetLine(i + 1).Length >= 9)
                            {
                                //And the line will contain header which begins with MTT
                                if (scintilla.GetLine(i + 1).Substring(9, 3) == "MTT")
                                {
                                    //If yes then take the line under it to the variable
                                    modifiers = scintilla.GetLine(i + 2);

                                    //If linefeed is CRLF, then two extra characters in the end of line
                                    if (scintilla.GetEOLMode() == 0)
                                    {
                                        lineFeedLen = 2;
                                    }
                                    else
                                    {
                                        lineFeedLen = 1;
                                    }

                                    //Removing lines which not needed anymore
                                    scintilla.SetCurrentPos(scintilla.PositionFromLine(i + 1));
                                    scintilla.LineDelete();
                                    scintilla.LineDelete();

                                    endLineNum = endLineNum - 2;

                                    //Determining which variables the modifiers line will contain
                                    if (modifiers.Length == (12 + lineFeedLen))
                                    {
                                        //Insert command to the line
                                        scintilla.InsertText(scintilla.PositionFromLine(i + 1), "C7GSC:TT=" + tt + ",NP=" + np + ",NA=" + ",NS=" + ns + ",MTT=" + modifiers.Substring(9, 3).Replace(" ", string.Empty) + ";");
                                    }
                                    else if (modifiers.Length == (17 + lineFeedLen))
                                    {
                                        if (!string.IsNullOrWhiteSpace(modifiers.Substring(9, 3)))
                                        {
                                            //Insert command to the line
                                            scintilla.InsertText(scintilla.GetCurrentPos(), "C7GSC:TT=" + tt + ",NP=" + np + ",NA=" + ",NS=" + ns + ",MTT=" + modifiers.Substring(9, 3).Replace(" ", string.Empty) + ";");
                                            //Go to end of the current line
                                            scintilla.GotoPos(scintilla.GetLineEndPosition(scintilla.LineFromPosition(scintilla.GetCurrentPos())));
                                            //Adding new line
                                            scintilla.NewLine();
                                            endLineNum++;
                                        }
                                        scintilla.InsertText(scintilla.GetCurrentPos(), "C7GSC:TT=" + tt + ",NP=" + np + ",NA=" + ",NS=" + ns + ",MNP=" + modifiers.Substring(14, 3).Replace(" ", string.Empty) + ";");
                                    }
                                    else if (modifiers.Length == (22 + lineFeedLen))
                                    {
                                        if (!string.IsNullOrWhiteSpace(modifiers.Substring(9, 3)))
                                        {
                                            scintilla.InsertText(scintilla.GetCurrentPos(), "C7GSC:TT=" + tt + ",NP=" + np + ",NA=" + ",NS=" + ns + ",MTT=" + modifiers.Substring(9, 3).Replace(" ", string.Empty) + ";");
                                            scintilla.GotoPos(scintilla.GetLineEndPosition(scintilla.LineFromPosition(scintilla.GetCurrentPos())));
                                            scintilla.NewLine();
                                            endLineNum++;
                                        }
                                        if (!string.IsNullOrWhiteSpace(modifiers.Substring(14, 3)))
                                        {
                                            scintilla.InsertText(scintilla.GetCurrentPos(), "C7GSC:TT=" + tt + ",NP=" + np + ",NA=" + ",NS=" + ns + ",MNP=" + modifiers.Substring(14, 3).Replace(" ", string.Empty) + ";");
                                            scintilla.GotoPos(scintilla.GetLineEndPosition(scintilla.LineFromPosition(scintilla.GetCurrentPos())));
                                            scintilla.NewLine();
                                            endLineNum++;
                                        }
                                        scintilla.InsertText(scintilla.GetCurrentPos(), "C7GSC:TT=" + tt + ",NP=" + np + ",NA=" + ",NS=" + ns + ",MNA=" + modifiers.Substring(19, 3).Replace(" ", string.Empty) + ";");
                                    }
                                    else
                                    {
                                        if (!string.IsNullOrWhiteSpace(modifiers.Substring(9, 3)))
                                        {
                                            scintilla.InsertText(scintilla.GetCurrentPos(), "C7GSC:TT=" + tt + ",NP=" + np + ",NA=" + ",NS=" + ns + ",MTT=" + modifiers.Substring(9, 3).Replace(" ", string.Empty) + ";");
                                            scintilla.GotoPos(scintilla.GetLineEndPosition(scintilla.LineFromPosition(scintilla.GetCurrentPos())));
                                            scintilla.NewLine();
                                            endLineNum++;
                                        }
                                        if (!string.IsNullOrWhiteSpace(modifiers.Substring(14, 3)))
                                        {
                                            scintilla.InsertText(scintilla.GetCurrentPos(), "C7GSC:TT=" + tt + ",NP=" + np + ",NA=" + ",NS=" + ns + ",MNP=" + modifiers.Substring(14, 3).Replace(" ", string.Empty) + ";");
                                            scintilla.GotoPos(scintilla.GetLineEndPosition(scintilla.LineFromPosition(scintilla.GetCurrentPos())));
                                            scintilla.NewLine();
                                            endLineNum++;
                                        }
                                        if (!string.IsNullOrWhiteSpace(modifiers.Substring(19, 3)))
                                        {
                                            scintilla.InsertText(scintilla.GetCurrentPos(), "C7GSC:TT=" + tt + ",NP=" + np + ",NA=" + ",NS=" + ns + ",MNA=" + modifiers.Substring(19, 3).Replace(" ", string.Empty) + ";");
                                            scintilla.GotoPos(scintilla.GetLineEndPosition(scintilla.LineFromPosition(scintilla.GetCurrentPos())));
                                            scintilla.NewLine();
                                            endLineNum++;
                                        }
                                        scintilla.InsertText(scintilla.GetCurrentPos(), "C7GSC:TT=" + tt + ",NP=" + np + ",NA=" + ",NS=" + ns + ",MNS=" + modifiers.Substring(24, (modifiers.Length - 24 - lineFeedLen)).Replace(" ", string.Empty) + ";");
                                    }
                                }
                            }
                        }
                    }

                    i++;
                }
            }
            scintilla.EndUndoAction();
        }
    }
}
