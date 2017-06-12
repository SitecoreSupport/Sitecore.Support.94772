using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.IO;
using Sitecore.Shell.Applications.Install;
using Sitecore.Text;
using Sitecore.Web;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Pages;
using Sitecore.Web.UI.Sheer;
using Sitecore.Web.UI.XmlControls;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.UI;

namespace Sitecore.Support.Shell.Applications.Install.Dialogs
{
    public class UploadPackageForm : WizardForm
    {
        protected Scrollbox FileList;

        protected XmlControl Location;

        protected Checkbox OverwriteCheck;

        protected string Directory
        {
            get
            {
                return StringUtil.GetString(Context.ClientPage.ServerProperties["Directory"]);
            }
            set
            {
                Assert.ArgumentNotNull(value, "value");
                Context.ClientPage.ServerProperties["Directory"] = value;
            }
        }

        protected override void ActivePageChanged(string page, string oldPage)
        {
            Assert.ArgumentNotNull(page, "page");
            Assert.ArgumentNotNull(oldPage, "oldPage");
            base.ActivePageChanged(page, oldPage);
            if (page == "Uploading")
            {
                this.NextButton.Disabled = true;
                this.BackButton.Disabled = true;
                this.CancelButton.Disabled = true;
                Context.ClientPage.ClientResponse.SetAttribute("Path", "value", FileHandle.GetFileHandle(this.Directory));
                Context.ClientPage.ClientResponse.SetAttribute("Overwrite", "value", this.OverwriteCheck.Checked ? "1" : "0");
                Context.ClientPage.ClientResponse.Timer("StartUploading", 10);
            }
            if (page == "LastPage")
            {
                this.NextButton.Disabled = true;
                this.BackButton.Disabled = true;
                this.CancelButton.Disabled = true;
                this.CancelButton.Disabled = false;
            }
        }

        protected override bool ActivePageChanging(string page, ref string newpage)
        {
            Assert.ArgumentNotNull(page, "page");
            Assert.ArgumentNotNull(newpage, "newpage");
            bool result;
            if (page == "Files" && newpage == "Settings")
            {
                if (!this.GetFileList().Any<string>())
                {
                    Context.ClientPage.ClientResponse.Alert("Please specify at least one file to upload.");
                    result = false;
                    return result;
                }
                string text = this.GetInvalidFileNames().FirstOrDefault<string>();
                if (!string.IsNullOrEmpty(text))
                {
                    this.ShowInvalidFileMessage(text);
                    this.NextButton.Disabled = true;
                    result = false;
                    return result;
                }
            }
            if (page == "Retry")
            {
                newpage = "Settings";
            }
            result = base.ActivePageChanging(page, ref newpage);
            return result;
        }

        private string BuildFileInput()
        {
            string uniqueID = Sitecore.Web.UI.HtmlControls.Control.GetUniqueID("File");
            Context.ClientPage.ServerProperties["LastFileID"] = uniqueID;
            string clientEvent = Context.ClientPage.GetClientEvent("FileChange");
            return string.Concat(new string[]
            {
                "<input id=\"",
                uniqueID,
                "\" name=\"",
                uniqueID,
                "\" type=\"file\" value=\"browse\" style=\"width:100%\" onchange=\"",
                clientEvent,
                "\"/>"
            });
        }

        protected void EndUploading(string fileName)
        {
            Assert.ArgumentNotNull(fileName, "fileName");
            Context.ClientPage.ClientResponse.SetDialogValue("ok:" + fileName);
            base.Next();
        }

        protected override void EndWizard()
        {
            Context.ClientPage.ClientResponse.Eval("window.top.close()");
        }

        protected void FileChange()
        {
            string text = this.GetInvalidFileNames().FirstOrDefault<string>();
            if (!string.IsNullOrEmpty(text))
            {
                this.ShowInvalidFileMessage(text);
                this.NextButton.Disabled = true;
                Context.ClientPage.ClientResponse.SetReturnValue(false);
            }
            else
            {
                this.NextButton.Disabled = false;
                string value = Context.ClientPage.ClientRequest.Form[Context.ClientPage.ClientRequest.Source];
                if (Context.ClientPage.ClientRequest.Source == StringUtil.GetString(Context.ClientPage.ServerProperties["LastFileID"]) && !string.IsNullOrEmpty(value))
                {
                    string text2 = this.BuildFileInput();
                    Context.ClientPage.ClientResponse.Insert("FileList", "beforeEnd", text2);
                }
                Context.ClientPage.ClientResponse.SetReturnValue(true);
            }
        }

        private IEnumerable<string> GetFileList()
        {
            IEnumerator enumerator = Context.ClientPage.ClientRequest.Form.Keys.GetEnumerator();
            while (enumerator.MoveNext())
            {
                string text = (string)enumerator.Current;
                if (text != null && text.StartsWith("File", StringComparison.InvariantCulture))
                {
                    string text2 = Context.ClientPage.ClientRequest.Form[text];
                    if (!string.IsNullOrEmpty(text2))
                    {
                        yield return text2;
                    }
                }
            }
            yield break;
        }

        private IEnumerable<string> GetInvalidFileNames()
        {
            return from s in this.GetFileList()
                   where !this.ValidateZipOrXmlFile(s)
                   select s;
        }

        private static string GetUrl(string directory)
        {
            Assert.ArgumentNotNull(directory, "directory");
            UrlString urlString = new UrlString("/sitecore/shell/applications/install/dialogs/Upload Package/UploadPackage.aspx");
            urlString.Append("di", ApplicationContext.StoreObject(directory));
            return urlString.ToString();
        }

        public override void HandleMessage(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            base.HandleMessage(message);
            if (message.Name == "packager:endupload")
            {
                string text = message.Arguments["filename"];
                string filename = FileHandle.GetFilename(text);
                if (!string.IsNullOrEmpty(filename))
                {
                    text = filename;
                }
                this.EndUploading(Path.GetFileName(text));
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            Assert.ArgumentNotNull(e, "e");
            if (!Context.ClientPage.IsEvent && !Context.ClientPage.IsPostBack)
            {
                string queryString = WebUtil.GetQueryString("di");
                if (queryString.Length > 0)
                {
                    this.Directory = StringUtil.GetString(ApplicationContext.RetrieveObject(queryString));
                }
                if (this.Directory.Length == 0)
                {
                    this.Directory = ApplicationContext.PackagePath;
                }
                string text = this.BuildFileInput();
                this.FileList.Controls.Add(new LiteralControl(text));
            }
            base.OnLoad(e);
        }

        public static void Show(string directory, bool postback)
        {
            Assert.ArgumentNotNull(directory, "directory");
            Context.ClientPage.ClientResponse.ShowModalDialog(UploadPackageForm.GetUrl(directory), postback);
        }

        public static void Show(string directory, string message)
        {
            Assert.ArgumentNotNull(directory, "directory");
            Assert.ArgumentNotNull(message, "message");
            Context.ClientPage.ClientResponse.ShowModalDialog(UploadPackageForm.GetUrl(directory), message);
        }

        protected void ShowInvalidFileMessage(string fileName)
        {
            Context.ClientPage.ClientResponse.Alert(Translate.Text("\"{0}\" is not a ZIP file.", new object[]
            {
                fileName
            }));
        }

        protected void StartUploading()
        {
            Context.ClientPage.ClientResponse.Eval("document.forms[0].submit()");
        }

        protected bool ValidateZipOrXmlFile(string fileName)
        {
            bool result;
            try
            {
                string extension = FileUtil.GetExtension(fileName);
                if (string.IsNullOrEmpty(extension))
                {
                    result = false;
                    return result;
                }
                if (string.Compare(extension, "zip", StringComparison.InvariantCultureIgnoreCase) == 0 || string.Compare(extension, "xml", StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    result = true;
                    return result;
                }
            }
            catch (Exception)
            {
            }
            result = false;
            return result;
        }
    }
}
