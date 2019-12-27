using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RoslynTypeScript.Translation;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using TypescriptSyntaxPaste;
using TypescriptSyntaxPaste.VSIX;

namespace CodeConverter
{
    public partial class frmMain : Form
    {
        public frmMain()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var ofd = new FolderBrowserDialog();
            ofd.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                this.txtPath.Text = ofd.SelectedPath;
                this.GetFilesFromDirectory();
            }
        }

        private void GetFilesFromDirectory()
        {
            var files = Directory.GetFiles(this.txtPath.Text, "*.cs", this.chkAllDirectory.Checked ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            this.itemsListBox.Items.AddRange(files);
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            if (this.itemsListBox.Items.Count > 0)
                for (int i = 0; i < this.itemsListBox.Items.Count; i++)
                {
                    this.itemsListBox.SetItemChecked(i, this.checkBox2.Checked);
                }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.ConvertFiles(this.itemsListBox.CheckedItems.Cast<string>());
        }

        private void ConvertFiles(IEnumerable<string> items)
        {
            foreach (var item in items)
            {
                var text = File.ReadAllText(item);
                var extracted = this.ConvertToTypescript(text, new ConvertSettings() { IsConvertToInterface = true, IsConvertMemberToCamelCase = true , IsConvertListToArray = true });
                File.WriteAllText(item.Replace(".cs", ".ts"), extracted);
                this.txtLog.AppendText($"Item: '{item}', exported\n");
            }
        }

        public string ConvertToTypescript(string text, ConvertSettings settingStore)
        {
            try
            {
                CSharpSyntaxTree text1 = (CSharpSyntaxTree)CSharpSyntaxTree.ParseText(text, null, "", null, new CancellationToken());
                if (text1.GetDiagnostics(new CancellationToken()).Any(f => f.Severity == (DiagnosticSeverity)3))
                    return null;
                CSharpSyntaxNode syntaxNode = text1.GetRoot(new CancellationToken());
                if (this.IsEmptyRoot(syntaxNode))
                    return null;

                if (settingStore.IsConvertToInterface)
                    syntaxNode = ClassToInterfaceReplacement.ReplaceClass(syntaxNode);

                if (settingStore.IsConvertMemberToCamelCase)
                    syntaxNode = MakeMemberCamelCase.Make(syntaxNode);

                if (settingStore.IsConvertListToArray)
                    syntaxNode = ListToArrayReplacement.ReplaceList(syntaxNode);

                if (settingStore.ReplacedTypeNameArray.Length != 0)
                    syntaxNode = TypeNameReplacement.Replace(settingStore.ReplacedTypeNameArray, syntaxNode);

                if (settingStore.AddIPrefixInterfaceDeclaration)
                    syntaxNode = AddIPrefixInterfaceDeclaration.AddIPrefix(syntaxNode);

                if (settingStore.IsInterfaceOptionalProperties)
                    syntaxNode = OptionalInterfaceProperties.AddOptional(syntaxNode);

                CSharpSyntaxTree syntaxTree = (CSharpSyntaxTree)syntaxNode.SyntaxTree;
                SyntaxTranslation syntaxTranslation = TF.Get(syntaxNode, null);
                CSharpCompilation csharpCompilation = CSharpCompilation.Create("TemporaryCompilation", new[] { syntaxTree });
                SemanticModel semanticModel = ((Compilation)csharpCompilation).GetSemanticModel(syntaxTree, false);
                syntaxTranslation.Compilation = csharpCompilation;
                syntaxTranslation.SemanticModel = semanticModel;
                syntaxTranslation.ApplyPatch();
                return syntaxTranslation.Translate();
            }
            catch (Exception ex)
            {
            }
            return (string)null;
        }

        private bool IsEmptyRoot(SyntaxNode root)
        {
            return !root.DescendantNodes(null, false).Any();
        }
    }


    public class ConvertSettings
    {
        public ConvertSettings()
        {
            this.ReplacedTypeNameArray = new TypeNameReplacementData[0];
        }
        public bool IsConvertMemberToCamelCase { get; set; }
        public bool IsConvertToInterface { get; set; }
        public bool IsConvertListToArray { get; set; }
        public bool IsInterfaceOptionalProperties { get; set; }
        public TypeNameReplacementData[] ReplacedTypeNameArray { get; set; }
        public bool AddIPrefixInterfaceDeclaration { get; set; }
    }
}
