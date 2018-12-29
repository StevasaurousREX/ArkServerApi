﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ArkApi_Hook_Creator
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        Dictionary<int, Dictionary<string, Dictionary<int, string>>> FunctionInfo = new Dictionary<int, Dictionary<string, Dictionary<int, string>>>();
        Dictionary<string, Dictionary<int, string>> StructureSelector = new Dictionary<string, Dictionary<int, string>>();
        Dictionary<int, string> FunctionSelector = new Dictionary<int, string>();
        Dictionary<int, int> FunctionIndexer = new Dictionary<int, int>();

        private void AddFunction(int ClassIndex, string Structure, int FunctionIndex, string Function)
        {
            if (FunctionInfo.TryGetValue(ClassIndex, out StructureSelector))
            {
                if (StructureSelector.TryGetValue(Structure, out FunctionSelector)) FunctionSelector.Add(FunctionIndex, Function);
                else StructureSelector.Add(Structure, new Dictionary<int, string> { { FunctionIndex, Function } });
            }
            else FunctionInfo.Add(ClassIndex, new Dictionary<string, Dictionary<int, string>> { { Structure, new Dictionary<int, string> { { FunctionIndex, Function } } } });
        }

        private void Form1_Load(object sender, EventArgs e)
        {
             ClassCombo.Items.AddRange(new string[] { "Actor", "GameMode", "GameState", "Inventory", "Other", "PrimalStructure", "Tribe" });
             int ClassIndex = 0, ClassCount = ClassCombo.Items.Count-1;
             foreach (string ArkHeader in ClassCombo.Items)
             {
                 using (WebClient wc = new WebClient())
                 {
                     int ClassId = ClassIndex++;
                     wc.DownloadStringCompleted += (object s, DownloadStringCompletedEventArgs ea) => ParseArkHeader(ClassId, ClassId == ClassCount, s, ea);
                     wc.DownloadStringAsync(new Uri("https://raw.githubusercontent.com/Michidu/ARK-Server-API/master/version/Core/Public/API/ARK/" + ArkHeader + ".h"));
                 }
             }
        }

        private void ParseArkHeader(int ClassIndex, bool Completed, object sender, DownloadStringCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                MessageBox.Show("Error: " + e.Error.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string HtmlData = e.Result;
            int FindIndex = -1, StructureIndex = 0, FunctionIndex = 0, indexof = HtmlData.IndexOf("	struct ");
            //Remove structures within structures
            if (indexof != -1)
            {
                int indexofend = HtmlData.IndexOf('}', indexof);
                while(indexof != -1)
                {
                    HtmlData = HtmlData.Remove(indexof, indexofend - indexof + 2);
                    indexof = HtmlData.IndexOf("	struct ");
                    if (indexof != -1) indexofend = HtmlData.IndexOf('}', indexof);
                }
            }

            string StructName = "";
            string[] splts = Regex.Split(HtmlData, "struct ");
            for (int i = 1; i < splts.Length; i++)
            {
                //Structure Name
                FindIndex = splts[i].IndexOf("\n");
                StructName = splts[i].Substring(0, FindIndex);
                if ((FindIndex = StructName.IndexOf(" :")) != -1) StructName = StructName.Substring(0, FindIndex);

                //Find Functions
                if ((FindIndex = splts[i].IndexOf("// Functions")) != -1)
                {
                    FindIndex += 15;
                    string[] splts2 = splts[i].Substring(FindIndex, splts[i].Length - FindIndex).Split('\n');
                    foreach (string s in splts2)
                        if (s.Length > 5)
                            AddFunction(ClassIndex, StructName.Replace(" * ", "* ").Replace("__declspec(align(8)) ", ""), FunctionIndex++, s.Replace("\t", ""));
                    FunctionIndex = 0;
                    StructureIndex++;
                }
            }

            if(Completed) ClassCombo.Enabled = StructCombo.Enabled = true;
        }

        private void ClassCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            StructCombo.Items.Clear();
            if (FunctionInfo.TryGetValue(ClassCombo.SelectedIndex, out StructureSelector))
                foreach (KeyValuePair<string, Dictionary<int, string>> StructShit in StructureSelector)
                    StructCombo.Items.Add(StructShit.Key);
        }

        private void StructCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            FunctionIndexer.Clear();
            FuncCombo.Items.Clear();
            FuncCombo.Enabled = true;
            FuncCombo.Text = "";
            if (FunctionInfo.TryGetValue(ClassCombo.SelectedIndex, out StructureSelector) && StructureSelector.TryGetValue(StructCombo.Text, out FunctionSelector))
                foreach (KeyValuePair<int, string> func in FunctionSelector)
                {
                    FunctionIndexer.Add(FuncCombo.Items.Count, func.Key);
                    if (func.Value.Contains(" { ")) FuncCombo.Items.Add(Regex.Split(func.Value, " { ")[0].Replace(" * ", "* "));
                    else FuncCombo.Items.Add(func.Value.Replace(" * ", "* "));
                }
        }

        private string LowerCase(string str)
        {
            return string.IsNullOrEmpty(str) ? str : char.IsLower(str, 0) ? (char.ToUpperInvariant(str[0]) + str.Substring(1)) : (char.ToLowerInvariant(str[0]) + str.Substring(1));
        }

        private void FuncCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            richTextBox1.Clear();
            if (FunctionInfo.TryGetValue(ClassCombo.SelectedIndex, out StructureSelector) && StructureSelector.TryGetValue(StructCombo.Text, out FunctionSelector) 
            && FunctionIndexer.TryGetValue(FuncCombo.SelectedIndex, out int FuncIndex) && FunctionSelector.TryGetValue(FuncIndex, out string FunctionData)
            && FunctionData.Contains("NativeCall<"))
            {
                string FunctionVariables = Regex.Split(FunctionData, "NativeCall<")[1];
                FunctionVariables = FunctionVariables.Substring(0, FunctionVariables.IndexOf('(') - 1).Replace(" * ", "* ");
                string FriendlyHookName = FuncCombo.Text;
                FriendlyHookName = FriendlyHookName.Replace("* >", "*>").Replace(" *>", "*>").Replace("()", "").Replace(")", "");
                if (FriendlyHookName.Contains(" ")) FriendlyHookName = FriendlyHookName.Split(' ')[1];
                if (FriendlyHookName.Contains("(")) FriendlyHookName = FriendlyHookName.Split('(')[0];
                string Hook = "DECLARE_HOOK(" + StructCombo.Text + "_" + FriendlyHookName;
                string HookFunc = "";
                // DECLARE_HOOK ARGS
                if (FunctionVariables.Contains(", "))
                {
                    bool AddedClass = false;
                    string[] Vars = Regex.Split(FunctionVariables, ", ");
                    foreach (string s in Vars)
                    {
                        Hook += ", " + s;
                        if (!AddedClass) //Add Structure to args
                        {
                            Hook += ", " + StructCombo.Text + "*";
                            AddedClass = true;
                        }
                    }
                    HookFunc = Vars[0] + " ";
                }
                else
                {                                   //Add Structure to args
                    Hook += ", " + FunctionVariables + ", " + StructCombo.Text + "*";
                    HookFunc = FunctionVariables + " ";
                }
                Hook += ");";
                HookFunc += " Hook_" + StructCombo.Text + "_" + FriendlyHookName + "(" + StructCombo.Text + "* _this";
                string Variables = FuncCombo.Text;
                int FindIndex;
                if ((FindIndex = Variables.IndexOf('(')) != -1)
                {
                    Variables = Variables.Remove(0, FindIndex + 1);
                    FindIndex = Variables.IndexOf(')');
                    Variables = Variables.Substring(0, FindIndex + 1).Replace(" **", "**").Replace(" *", "*").Replace("enum ", "enum'");
                    HookFunc += (Variables.Length > 1 ? ", " + Variables.Replace("enum'", "enum ") : ")") + "\n{\n" + (HookFunc.StartsWith("void") ? "    " : "   return ");
                    HookFunc += StructCombo.Text + "_" + FriendlyHookName + "_original(_this";
                    if (Variables.Length > 1)
                    {
                        string[] Vars = Regex.Split(Variables, ", ");
                        foreach (string s in Vars)
                            if (s.Contains(" ")) HookFunc += ", " + s.Split(' ')[1].Replace(")", "");
                        HookFunc += ");\n}";
                    }
                    else HookFunc += ");\n}";
                    richTextBox1.AppendText(Hook + Environment.NewLine + Environment.NewLine + HookFunc + Environment.NewLine + Environment.NewLine
                        + "ArkApi::GetHooks().SetHook(\"" + StructCombo.Text + "." + FriendlyHookName + "\", &Hook_" + StructCombo.Text + "_" + FriendlyHookName + ", &" + StructCombo.Text + "_" + FriendlyHookName + "_original);" + Environment.NewLine + Environment.NewLine
                        + "ArkApi::GetHooks().DisableHook(\"" + StructCombo.Text + "." + FriendlyHookName + "\", &Hook_" + StructCombo.Text + "_" + FriendlyHookName + ");");
                    Clipboard.SetText(richTextBox1.Text);
                }
            }
        }

        private void FuncCombo_TextUpdate(object sender, EventArgs e)
        {
            if(ClassCombo.SelectedIndex == -1)
            {
                FuncCombo.Text = "";
                MessageBox.Show("Please Select a Class First!", "Class Not Selected!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            if (StructCombo.SelectedIndex == -1)
            {
                FuncCombo.Text = "";
                MessageBox.Show("Please Select a Structure First!", "Structure Not Selected!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            FuncCombo.Items.Clear();
            FunctionIndexer.Clear();
            if (FunctionInfo.TryGetValue(ClassCombo.SelectedIndex, out StructureSelector) && StructureSelector.TryGetValue(StructCombo.Text, out FunctionSelector))
            {
                string FuncName;
                foreach (KeyValuePair<int, string> func in FunctionSelector)
                {
                    if (func.Value.Contains(" { "))
                    {
                        FuncName = Regex.Split(func.Value, " { ")[0].Replace(" * ", "* ");
                        if (FuncName.ToLower().Contains(FuncCombo.Text.ToLower()))
                        {
                            FunctionIndexer.Add(FuncCombo.Items.Count, func.Key);
                            FuncCombo.Items.Add(FuncName);
                        }
                    }
                    else
                    {
                        FuncName = func.Value.Replace(" * ", "* ");
                        if (FuncName.ToLower().Contains(FuncCombo.Text.ToLower()))
                        {
                            FunctionIndexer.Add(FuncCombo.Items.Count, func.Key);
                            FuncCombo.Items.Add(FuncName);
                        }
                    }
                }
            }
            FuncCombo.SelectionStart = FuncCombo.Text.Length;
            FuncCombo.SelectionLength = 0;
        }
    }
}