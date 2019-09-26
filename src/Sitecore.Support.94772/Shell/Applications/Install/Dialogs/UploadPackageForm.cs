using System;
using System.Web.UI;
using Sitecore.Diagnostics;
using Sitecore.Text;
using Sitecore.Web;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Pages;
using Sitecore.Web.UI.Sheer;
using Sitecore.Web.UI.XmlControls;

using System.Collections.Generic;
using System.Linq;
using Sitecore.Globalization;
using Sitecore.IO;
using Sitecore.Shell.Applications.Install;

namespace Sitecore.Support.Shell.Applications.Install.Dialogs
{
    public class UploadPackageForm : WizardForm
    {
        #region Controls

        /// <summary>
        /// The file list
        /// </summary>
        protected Scrollbox FileList;

        /// <summary>
        /// The overwrite check
        /// </summary>
        protected Checkbox OverwriteCheck;

        /// <summary>
        /// The location
        /// </summary>
        protected XmlControl Location;

        #endregion

        /// <summary>
        /// Gets or sets the directory.
        /// </summary>
        /// <value>
        /// The directory.
        /// </value>
        [NotNull]
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

        #region Protected methods

        /// <summary>
        /// Raises the load event.
        /// </summary>
        /// <param name="e">The <see cref="System.EventArgs" /> instance containing the event data.</param>
        /// <remarks>
        /// This method notifies the server control that it should perform actions common to each HTTP
        /// request for the page it is associated with, such as setting up a database query. At this
        /// stage in the page lifecycle, server controls in the hierarchy are created and initialized,
        /// view state is restored, and form controls reflect client-side data. Use the IsPostBack
        /// property to determine whether the page is being loaded in response to a client postback,
        /// or if it is being loaded and accessed for the first time.
        /// </remarks>
        protected override void OnLoad(EventArgs e)
        {
            Assert.ArgumentNotNull(e, "e");

            if (!Context.ClientPage.IsEvent && !Context.ClientPage.IsPostBack)
            {
                string directory = WebUtil.GetQueryString("di");

                if (directory.Length > 0)
                {
                    this.Directory = StringUtil.GetString(ApplicationContext.RetrieveObject(directory));
                }

                if (this.Directory.Length == 0)
                {
                    this.Directory = ApplicationContext.PackagePath;
                }

                string input = this.BuildFileInput();

                this.FileList.Controls.Add(new LiteralControl(input));
            }

            base.OnLoad(e);
        }

        /// <summary>
        /// Called when the active page is changing.
        /// </summary>
        /// <param name="page">The page that is being left.</param>
        /// <param name="newpage">The new page that is being entered.</param>
        /// <returns>
        /// True, if the change is allowed, otherwise false.
        /// </returns>
        /// <contract>
        ///   <requires name="page" condition="not null" />
        ///   <requires name="newpage" condition="not null" />
        /// </contract>
        /// <remarks>
        /// Set the newpage parameter to another page ID to control the
        /// path through the wizard pages.
        /// </remarks>
        protected override bool ActivePageChanging(string page, ref string newpage)
        {
            Assert.ArgumentNotNull(page, "page");
            Assert.ArgumentNotNull(newpage, "newpage");

            if (page == "Files" && newpage == "Settings")
            {
                bool hasFiles = this.GetFileList().Any();

                if (!hasFiles)
                {
                    Context.ClientPage.ClientResponse.Alert(Texts.PleaseSpecifyAtLeastOneFileToUpload);
                    return false;
                }

                string invalidFile = this.GetInvalidFileNames().FirstOrDefault();
                if (!string.IsNullOrEmpty(invalidFile))
                {
                    this.ShowInvalidFileMessage(invalidFile);
                    NextButton.Disabled = true;
                    return false;
                }
            }

            if (page == "Retry")
            {
                newpage = "Settings";
            }

            return base.ActivePageChanging(page, ref newpage);
        }

        /// <summary>
        /// Called when the active page has been changed.
        /// </summary>
        /// <param name="page">The page that has been entered.</param>
        /// <param name="oldPage">The page that was left.</param>
        /// <contract>
        ///   <requires name="page" condition="not null" />
        ///   <requires name="oldPage" condition="not null" />
        /// </contract>
        protected override void ActivePageChanged(string page, string oldPage)
        {
            Assert.ArgumentNotNull(page, "page");
            Assert.ArgumentNotNull(oldPage, "oldPage");

            base.ActivePageChanged(page, oldPage);

            this.NextButton.Header = Texts.NEXT;
            if (page == "Settings")
            {
                this.NextButton.Header = Texts.UPLOAD;
            }

            if (page == "Uploading")
            {
                NextButton.Disabled = true;
                BackButton.Disabled = true;
                CancelButton.Disabled = true;

                Context.ClientPage.ClientResponse.SetAttribute("Path", "value", FileHandle.GetFileHandle(this.Directory));

                Context.ClientPage.ClientResponse.SetAttribute("Overwrite", "value", this.OverwriteCheck.Checked ? "1" : "0");

                Context.ClientPage.ClientResponse.Timer("StartUploading", 10);
            }

            if (page == "LastPage")
            {
                NextButton.Disabled = true;
                BackButton.Disabled = true;

                // quick dirty hack
                CancelButton.Disabled = true;
                CancelButton.Disabled = false;
            }
        }

        /// <summary>
        /// Starts the uploading.
        /// </summary>
        protected void StartUploading()
        {
            Context.ClientPage.ClientResponse.Eval("submit()");
        }

        /// <summary>
        /// Ends the uploading.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        protected void EndUploading([NotNull] string fileName)
        {
            Assert.ArgumentNotNull(fileName, "fileName");

            Context.ClientPage.ClientResponse.SetDialogValue("ok:" + fileName);
            this.Next();
        }

        /// <summary>
        /// Shows the file has malicious content warning.
        /// </summary>
        /// <param name="fileName">The filename.</param>
        protected void ShowMaliciousFileWarning(string fileName)
        {
            Assert.ArgumentNotNullOrEmpty(fileName, nameof(fileName));

            SheerResponse.Alert(Translate.Text(Texts.TheFile0HasMaliciousContent, fileName));

            this.EndWizard();
        }

        /// <summary>
        /// Ends the wizard.
        /// </summary>
        protected override void EndWizard()
        {
            Context.ClientPage.ClientResponse.Eval("window.top.dialogClose()");
        }

        /// <summary>
        /// Files the change.
        /// </summary>
        protected void FileChange()
        {
            string invalidFile = this.GetInvalidFileNames().FirstOrDefault();
            if (!string.IsNullOrEmpty(invalidFile))
            {
                this.ShowInvalidFileMessage(invalidFile);
                this.NextButton.Disabled = true;
                Context.ClientPage.ClientResponse.SetReturnValue(false);
                return;
            }

            this.NextButton.Disabled = false;
            string value = Context.ClientPage.ClientRequest.Form[Context.ClientPage.ClientRequest.Source];

            if (Context.ClientPage.ClientRequest.Source == StringUtil.GetString(Context.ClientPage.ServerProperties["LastFileID"]))
            {

                if (!string.IsNullOrEmpty(value))
                {
                    string input = BuildFileInput();

                    Context.ClientPage.ClientResponse.Insert("FileList", "beforeEnd", input);
                }
            }

            Context.ClientPage.ClientResponse.SetReturnValue(true);
        }

        /// <summary>
        /// Gets the invalid file names.
        /// </summary>
        /// <returns></returns>
        [NotNull]
        private IEnumerable<string> GetInvalidFileNames()
        {
            return this.GetFileList().Where(s => !this.ValidateZipFile(s));
        }

        /// <summary>
        /// Gets the file list.
        /// </summary>
        /// <returns></returns>
        [NotNull]
        private IEnumerable<string> GetFileList()
        {
            foreach (string key in Context.ClientPage.ClientRequest.Form.Keys)
            {
                if (key != null && key.StartsWith("File", StringComparison.InvariantCulture))
                {
                    string file = Context.ClientPage.ClientRequest.Form[key];

                    if (!string.IsNullOrEmpty(file))
                    {
                        yield return file;
                    }
                }
            }
        }

        /// <summary>
        /// Shows the incorrect package.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        protected void ShowInvalidFileMessage(string fileName)
        {
            Context.ClientPage.ClientResponse.Alert(Translate.Text(Texts._0_IS_NOT_A_ZIP_File, fileName));
        }

        /// <summary>
        /// Validates the zip file.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <returns>
        /// Returns true if file has zip extension.
        /// </returns>
        protected bool ValidateZipFile(string fileName)
        {
            try
            {
                string extension = FileUtil.GetExtension(fileName);
                if (string.IsNullOrEmpty(extension))
                {
                    return false;
                }

                if (string.Compare(extension, "zip", StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    return true;
                }
            }
            catch (Exception)
            {
            }

            return false;
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Builds the file input.
        /// </summary>
        /// <returns>The file input.</returns>
        [NotNull]
        private string BuildFileInput()
        {
            string id = Sitecore.Web.UI.HtmlControls.Control.GetUniqueID("File");

            Context.ClientPage.ServerProperties["LastFileID"] = id;

            string change = Context.ClientPage.GetClientEvent("FileChange");

            string result = "<input id=\"" + id + "\" name=\"" + id + "\" type=\"file\" value=\"browse\" style=\"width:100%\" onchange=\"" + change + "\"/>";

            return result;
        }

        #endregion

        #region static API

        /// <summary>
        /// Gets the URL.
        /// </summary>
        /// <param name="directory">The directory.</param>
        /// <returns></returns>
        [NotNull]
        private static string GetUrl([NotNull] string directory)
        {
            Assert.ArgumentNotNull(directory, "directory");

            UrlString url = new UrlString("/sitecore/shell/applications/install/dialogs/Upload Package/UploadPackage.aspx");
            url.Append("di", ApplicationContext.StoreObject(directory));
            return url.ToString();
        }

        /// <summary>
        /// Shows the specified directory.
        /// </summary>
        /// <param name="directory">The directory.</param>
        /// <param name="postback">if set to <c>true</c> [postback].</param>
        public static void Show([NotNull] string directory, bool postback)
        {
            Assert.ArgumentNotNull(directory, "directory");

            Context.ClientPage.ClientResponse.ShowModalDialog(GetUrl(directory), postback);
        }

        /// <summary>
        /// Shows the specified directory.
        /// </summary>
        /// <param name="directory">The directory.</param>
        /// <param name="message">The message.</param>
        public static void Show([NotNull] string directory, [NotNull] string message)
        {
            Assert.ArgumentNotNull(directory, "directory");
            Assert.ArgumentNotNull(message, "message");

            Context.ClientPage.ClientResponse.ShowModalDialog(GetUrl(directory), message);
        }

        /// <summary>
        /// Shows the error.
        /// </summary>
        protected void ShowError()
        {
            Active = "Files";
            SheerResponse.Alert("An error occured while uploading the file.");
        }

        #endregion static API

        #region Public

        /// <summary>
        /// Handles the message.
        /// </summary>
        /// <param name="message">The message.</param>
        public override void HandleMessage([NotNull] Sitecore.Web.UI.Sheer.Message message)
        {
            Assert.ArgumentNotNull(message, "message");

            base.HandleMessage(message);

            if (message.Name == "packager:endupload")
            {
                string filename = message.Arguments["filename"];
                string fromHandle = FileHandle.GetFilename(filename);
                if (!string.IsNullOrEmpty(fromHandle))
                {
                    filename = fromHandle;
                }

                this.EndUploading(System.IO.Path.GetFileName(filename));
            }
        }

        #endregion
    }
}
