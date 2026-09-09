namespace Media_MKV_Converter.Gui;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel layoutRoot;
    private Label lblInputFolder;
    private TextBox txtInputFolder;
    private Button btnBrowseInput;
    private Label lblOutputFolder;
    private TextBox txtOutputFolder;
    private Button btnBrowseOutput;
    private CheckBox chkOverwrite;
    private Label lblSkip;
    private TextBox txtSkip;
    private Label lblLanguages;
    private CheckedListBox clbLanguages;
    private CheckBox chkDryRun;
    private CheckBox chkTraverseSubfolders;
    private CheckBox chkMaintainFolderStructure;
    private FlowLayoutPanel panelTopActions;
    private Button btnToggleAdvanced;
    private Button btnStart;
    private Button btnCancel;
    private Button btnCopyLog;
    private Button btnOpenOutputFolder;
    private Panel panelAdvanced;
    private TableLayoutPanel layoutAdvanced;
    private FlowLayoutPanel panelAdvancedOptions;
    private Label lblMkvMerge;
    private TextBox txtMkvMerge;
    private Button btnBrowseMkvMerge;
    private Label lblMkvPropEdit;
    private TextBox txtMkvPropEdit;
    private Button btnBrowseMkvPropEdit;
    private ProgressBar progressOverall;
    private Label lblProgressPercent;
    private Label lblCurrent;
    private Label lblSummary;
    private GroupBox grpResults;
    private TableLayoutPanel layoutResults;
    private Label lblResultsTotalCaption;
    private Label lblResultsSuccessCaption;
    private Label lblResultsSkippedCaption;
    private Label lblResultsFailedCaption;
    private Label lblResultsRuntimeCaption;
    private Label lblResultsTotalValue;
    private Label lblResultsSuccessValue;
    private Label lblResultsSkippedValue;
    private Label lblResultsFailedValue;
    private Label lblResultsRuntimeValue;
    private ListBox lstLog;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        layoutRoot = new TableLayoutPanel();
        lblInputFolder = new Label();
        txtInputFolder = new TextBox();
        btnBrowseInput = new Button();
        lblOutputFolder = new Label();
        txtOutputFolder = new TextBox();
        btnBrowseOutput = new Button();
        chkOverwrite = new CheckBox();
        lblSkip = new Label();
        txtSkip = new TextBox();
        lblLanguages = new Label();
        clbLanguages = new CheckedListBox();
        chkDryRun = new CheckBox();
        chkTraverseSubfolders = new CheckBox();
        chkMaintainFolderStructure = new CheckBox();
        panelTopActions = new FlowLayoutPanel();
        btnToggleAdvanced = new Button();
        btnStart = new Button();
        btnCancel = new Button();
        btnCopyLog = new Button();
        btnOpenOutputFolder = new Button();
        panelAdvanced = new Panel();
        layoutAdvanced = new TableLayoutPanel();
        panelAdvancedOptions = new FlowLayoutPanel();
        lblMkvMerge = new Label();
        txtMkvMerge = new TextBox();
        btnBrowseMkvMerge = new Button();
        lblMkvPropEdit = new Label();
        txtMkvPropEdit = new TextBox();
        btnBrowseMkvPropEdit = new Button();
        progressOverall = new ProgressBar();
        lblProgressPercent = new Label();
        lblCurrent = new Label();
        lblSummary = new Label();
        grpResults = new GroupBox();
        layoutResults = new TableLayoutPanel();
        lblResultsTotalCaption = new Label();
        lblResultsSuccessCaption = new Label();
        lblResultsSkippedCaption = new Label();
        lblResultsFailedCaption = new Label();
        lblResultsRuntimeCaption = new Label();
        lblResultsTotalValue = new Label();
        lblResultsSuccessValue = new Label();
        lblResultsSkippedValue = new Label();
        lblResultsFailedValue = new Label();
        lblResultsRuntimeValue = new Label();
        lstLog = new ListBox();
        layoutRoot.SuspendLayout();
        panelTopActions.SuspendLayout();
        panelAdvanced.SuspendLayout();
        layoutAdvanced.SuspendLayout();
        grpResults.SuspendLayout();
        layoutResults.SuspendLayout();
        SuspendLayout();
        // 
        // layoutRoot
        // 
        layoutRoot.ColumnCount = 3;
        layoutRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115F));
        layoutRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layoutRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
        layoutRoot.Controls.Add(lblInputFolder, 0, 0);
        layoutRoot.Controls.Add(txtInputFolder, 1, 0);
        layoutRoot.Controls.Add(btnBrowseInput, 2, 0);
        layoutRoot.Controls.Add(lblOutputFolder, 0, 1);
        layoutRoot.Controls.Add(txtOutputFolder, 1, 1);
        layoutRoot.Controls.Add(btnBrowseOutput, 2, 1);
        layoutRoot.Controls.Add(panelTopActions, 0, 2);
        layoutRoot.Controls.Add(panelAdvanced, 0, 3);
        layoutRoot.Controls.Add(lblSummary, 0, 4);
        layoutRoot.Controls.Add(progressOverall, 0, 5);
        layoutRoot.Controls.Add(lblProgressPercent, 2, 5);
        layoutRoot.Controls.Add(lblCurrent, 0, 6);
        layoutRoot.Controls.Add(grpResults, 0, 7);
        layoutRoot.Controls.Add(lstLog, 0, 8);
        layoutRoot.Dock = DockStyle.Fill;
        layoutRoot.Location = new Point(0, 0);
        layoutRoot.Name = "layoutRoot";
        layoutRoot.Padding = new Padding(12);
        layoutRoot.RowCount = 9;
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
        layoutRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layoutRoot.Size = new Size(1084, 661);
        layoutRoot.TabIndex = 0;
        layoutRoot.SetColumnSpan(panelTopActions, 3);
        layoutRoot.SetColumnSpan(panelAdvanced, 3);
        layoutRoot.SetColumnSpan(lblSummary, 3);
        layoutRoot.SetColumnSpan(lblCurrent, 3);
        layoutRoot.SetColumnSpan(grpResults, 3);
        layoutRoot.SetColumnSpan(lstLog, 3);
        layoutRoot.SetColumnSpan(progressOverall, 2);
        // 
        // lblInputFolder
        // 
        lblInputFolder.Anchor = AnchorStyles.Left;
        lblInputFolder.AutoSize = true;
        lblInputFolder.Location = new Point(15, 19);
        lblInputFolder.Name = "lblInputFolder";
        lblInputFolder.Size = new Size(73, 15);
        lblInputFolder.TabIndex = 0;
        lblInputFolder.Text = "Input Folder";
        // 
        // txtInputFolder
        // 
        txtInputFolder.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        txtInputFolder.Location = new Point(130, 15);
        txtInputFolder.Name = "txtInputFolder";
        txtInputFolder.Size = new Size(839, 23);
        txtInputFolder.TabIndex = 1;
        // 
        // btnBrowseInput
        // 
        btnBrowseInput.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        btnBrowseInput.Location = new Point(975, 14);
        btnBrowseInput.Name = "btnBrowseInput";
        btnBrowseInput.Size = new Size(94, 25);
        btnBrowseInput.TabIndex = 2;
        btnBrowseInput.Text = "Browse...";
        btnBrowseInput.UseVisualStyleBackColor = true;
        btnBrowseInput.Click += btnBrowseInput_Click;
        // 
        // lblOutputFolder
        // 
        lblOutputFolder.Anchor = AnchorStyles.Left;
        lblOutputFolder.AutoSize = true;
        lblOutputFolder.Location = new Point(15, 53);
        lblOutputFolder.Name = "lblOutputFolder";
        lblOutputFolder.Size = new Size(83, 15);
        lblOutputFolder.TabIndex = 3;
        lblOutputFolder.Text = "Output Folder";
        // 
        // txtOutputFolder
        // 
        txtOutputFolder.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        txtOutputFolder.Location = new Point(130, 49);
        txtOutputFolder.Name = "txtOutputFolder";
        txtOutputFolder.PlaceholderText = "Optional";
        txtOutputFolder.Size = new Size(839, 23);
        txtOutputFolder.TabIndex = 4;
        // 
        // btnBrowseOutput
        // 
        btnBrowseOutput.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        btnBrowseOutput.Location = new Point(975, 48);
        btnBrowseOutput.Name = "btnBrowseOutput";
        btnBrowseOutput.Size = new Size(94, 25);
        btnBrowseOutput.TabIndex = 5;
        btnBrowseOutput.Text = "Browse...";
        btnBrowseOutput.UseVisualStyleBackColor = true;
        btnBrowseOutput.Click += btnBrowseOutput_Click;
        // 
        // panelTopActions
        // 
        panelTopActions.Controls.Add(btnStart);
        panelTopActions.Controls.Add(btnToggleAdvanced);
        panelTopActions.Controls.Add(btnCancel);
        panelTopActions.Controls.Add(btnCopyLog);
        panelTopActions.Controls.Add(btnOpenOutputFolder);
        panelTopActions.Dock = DockStyle.Fill;
        panelTopActions.Location = new Point(15, 83);
        panelTopActions.Name = "panelTopActions";
        panelTopActions.Size = new Size(1054, 34);
        panelTopActions.TabIndex = 10;
        // 
        // btnToggleAdvanced
        // 
        btnToggleAdvanced.AutoSize = true;
        btnToggleAdvanced.Location = new Point(93, 3);
        btnToggleAdvanced.Name = "btnToggleAdvanced";
        btnToggleAdvanced.Size = new Size(102, 25);
        btnToggleAdvanced.TabIndex = 1;
        btnToggleAdvanced.Text = "Show Settings";
        btnToggleAdvanced.UseVisualStyleBackColor = true;
        btnToggleAdvanced.Click += btnToggleAdvanced_Click;
        // 
        // btnStart
        // 
        btnStart.Location = new Point(3, 3);
        btnStart.Name = "btnStart";
        btnStart.Size = new Size(84, 25);
        btnStart.TabIndex = 0;
        btnStart.Text = "Start";
        btnStart.UseVisualStyleBackColor = true;
        btnStart.Click += btnStart_Click;
        // 
        // btnCancel
        // 
        btnCancel.Location = new Point(201, 3);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(84, 25);
        btnCancel.TabIndex = 2;
        btnCancel.Text = "Cancel";
        btnCancel.UseVisualStyleBackColor = true;
        btnCancel.Click += btnCancel_Click;
        // 
        // btnCopyLog
        // 
        btnCopyLog.Location = new Point(291, 3);
        btnCopyLog.Name = "btnCopyLog";
        btnCopyLog.Size = new Size(84, 25);
        btnCopyLog.TabIndex = 3;
        btnCopyLog.Text = "Copy Log";
        btnCopyLog.UseVisualStyleBackColor = true;
        btnCopyLog.Click += btnCopyLog_Click;
        // 
        // btnOpenOutputFolder
        // 
        btnOpenOutputFolder.Location = new Point(381, 3);
        btnOpenOutputFolder.Name = "btnOpenOutputFolder";
        btnOpenOutputFolder.Size = new Size(132, 25);
        btnOpenOutputFolder.TabIndex = 4;
        btnOpenOutputFolder.Text = "Open Output Folder";
        btnOpenOutputFolder.UseVisualStyleBackColor = true;
        btnOpenOutputFolder.Click += btnOpenOutputFolder_Click;
        // 
        // panelAdvanced
        // 
        panelAdvanced.Controls.Add(layoutAdvanced);
        panelAdvanced.Dock = DockStyle.Fill;
        panelAdvanced.Location = new Point(15, 123);
        panelAdvanced.Name = "panelAdvanced";
        panelAdvanced.Size = new Size(1054, 222);
        panelAdvanced.TabIndex = 11;
        // 
        // layoutAdvanced
        // 
        layoutAdvanced.ColumnCount = 3;
        layoutAdvanced.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115F));
        layoutAdvanced.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layoutAdvanced.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
        layoutAdvanced.Controls.Add(lblMkvMerge, 0, 0);
        layoutAdvanced.Controls.Add(txtMkvMerge, 1, 0);
        layoutAdvanced.Controls.Add(btnBrowseMkvMerge, 2, 0);
        layoutAdvanced.Controls.Add(lblMkvPropEdit, 0, 1);
        layoutAdvanced.Controls.Add(txtMkvPropEdit, 1, 1);
        layoutAdvanced.Controls.Add(btnBrowseMkvPropEdit, 2, 1);
        layoutAdvanced.Controls.Add(lblSkip, 0, 2);
        layoutAdvanced.Controls.Add(txtSkip, 1, 2);
        layoutAdvanced.Controls.Add(lblLanguages, 0, 3);
        layoutAdvanced.Controls.Add(clbLanguages, 1, 3);
        layoutAdvanced.Controls.Add(panelAdvancedOptions, 1, 4);
        layoutAdvanced.Dock = DockStyle.Fill;
        layoutAdvanced.Location = new Point(0, 0);
        layoutAdvanced.Name = "layoutAdvanced";
        layoutAdvanced.RowCount = 5;
        layoutAdvanced.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        layoutAdvanced.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        layoutAdvanced.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        layoutAdvanced.RowStyles.Add(new RowStyle(SizeType.Absolute, 86F));
        layoutAdvanced.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        layoutAdvanced.Size = new Size(1054, 222);
        layoutAdvanced.TabIndex = 0;
        layoutAdvanced.SetColumnSpan(panelAdvancedOptions, 2);
        layoutAdvanced.SetColumnSpan(clbLanguages, 2);
        // 
        // panelAdvancedOptions
        // 
        panelAdvancedOptions.Controls.Add(chkOverwrite);
        panelAdvancedOptions.Controls.Add(chkDryRun);
        panelAdvancedOptions.Controls.Add(chkTraverseSubfolders);
        panelAdvancedOptions.Controls.Add(chkMaintainFolderStructure);
        panelAdvancedOptions.Dock = DockStyle.Fill;
        panelAdvancedOptions.Location = new Point(118, 105);
        panelAdvancedOptions.Name = "panelAdvancedOptions";
        panelAdvancedOptions.Size = new Size(933, 44);
        panelAdvancedOptions.TabIndex = 10;
        // 
        // lblMkvMerge
        // 
        lblMkvMerge.Anchor = AnchorStyles.Left;
        lblMkvMerge.AutoSize = true;
        lblMkvMerge.Location = new Point(3, 9);
        lblMkvMerge.Name = "lblMkvMerge";
        lblMkvMerge.Size = new Size(83, 15);
        lblMkvMerge.TabIndex = 0;
        lblMkvMerge.Text = "mkvmerge.exe";
        // 
        // txtMkvMerge
        // 
        txtMkvMerge.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        txtMkvMerge.Location = new Point(118, 5);
        txtMkvMerge.Name = "txtMkvMerge";
        txtMkvMerge.Size = new Size(833, 23);
        txtMkvMerge.TabIndex = 1;
        // 
        // btnBrowseMkvMerge
        // 
        btnBrowseMkvMerge.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        btnBrowseMkvMerge.Location = new Point(957, 4);
        btnBrowseMkvMerge.Name = "btnBrowseMkvMerge";
        btnBrowseMkvMerge.Size = new Size(94, 25);
        btnBrowseMkvMerge.TabIndex = 2;
        btnBrowseMkvMerge.Text = "Browse...";
        btnBrowseMkvMerge.UseVisualStyleBackColor = true;
        btnBrowseMkvMerge.Click += btnBrowseMkvMerge_Click;
        // 
        // lblMkvPropEdit
        // 
        lblMkvPropEdit.Anchor = AnchorStyles.Left;
        lblMkvPropEdit.AutoSize = true;
        lblMkvPropEdit.Location = new Point(3, 43);
        lblMkvPropEdit.Name = "lblMkvPropEdit";
        lblMkvPropEdit.Size = new Size(90, 15);
        lblMkvPropEdit.TabIndex = 3;
        lblMkvPropEdit.Text = "mkvpropedit.exe";
        // 
        // txtMkvPropEdit
        // 
        txtMkvPropEdit.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        txtMkvPropEdit.Location = new Point(118, 39);
        txtMkvPropEdit.Name = "txtMkvPropEdit";
        txtMkvPropEdit.Size = new Size(833, 23);
        txtMkvPropEdit.TabIndex = 4;
        // 
        // btnBrowseMkvPropEdit
        // 
        btnBrowseMkvPropEdit.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        btnBrowseMkvPropEdit.Location = new Point(957, 38);
        btnBrowseMkvPropEdit.Name = "btnBrowseMkvPropEdit";
        btnBrowseMkvPropEdit.Size = new Size(94, 25);
        btnBrowseMkvPropEdit.TabIndex = 5;
        btnBrowseMkvPropEdit.Text = "Browse...";
        btnBrowseMkvPropEdit.UseVisualStyleBackColor = true;
        btnBrowseMkvPropEdit.Click += btnBrowseMkvPropEdit_Click;
        // 
        // lblSkip
        // 
        lblSkip.Anchor = AnchorStyles.Left;
        lblSkip.AutoSize = true;
        lblSkip.Location = new Point(3, 77);
        lblSkip.Name = "lblSkip";
        lblSkip.Size = new Size(82, 15);
        lblSkip.TabIndex = 6;
        lblSkip.Text = "Skip Directory";
        // 
        // txtSkip
        // 
        txtSkip.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        txtSkip.Location = new Point(118, 73);
        txtSkip.Name = "txtSkip";
        txtSkip.Size = new Size(833, 23);
        txtSkip.TabIndex = 7;
        //
        // lblLanguages
        //
        lblLanguages.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        lblLanguages.AutoSize = true;
        lblLanguages.Location = new Point(3, 111);
        lblLanguages.Name = "lblLanguages";
        lblLanguages.Padding = new Padding(0, 4, 0, 0);
        lblLanguages.Size = new Size(64, 19);
        lblLanguages.TabIndex = 8;
        lblLanguages.Text = "Languages";
        //
        // clbLanguages
        //
        clbLanguages.CheckOnClick = true;
        clbLanguages.ColumnWidth = 130;
        clbLanguages.Dock = DockStyle.Fill;
        clbLanguages.IntegralHeight = false;
        clbLanguages.Location = new Point(118, 110);
        clbLanguages.Margin = new Padding(3, 3, 3, 6);
        clbLanguages.MultiColumn = true;
        clbLanguages.Name = "clbLanguages";
        clbLanguages.Size = new Size(933, 77);
        clbLanguages.TabIndex = 9;
        //
        // chkOverwrite
        // 
        chkOverwrite.Anchor = AnchorStyles.Left;
        chkOverwrite.AutoSize = true;
        chkOverwrite.Checked = true;
        chkOverwrite.CheckState = CheckState.Checked;
        chkOverwrite.Location = new Point(3, 3);
        chkOverwrite.Name = "chkOverwrite";
        chkOverwrite.Size = new Size(79, 19);
        chkOverwrite.TabIndex = 0;
        chkOverwrite.Text = "Overwrite";
        chkOverwrite.UseVisualStyleBackColor = true;
        // 
        // chkDryRun
        // 
        chkDryRun.Anchor = AnchorStyles.Left;
        chkDryRun.AutoSize = true;
        chkDryRun.Location = new Point(88, 3);
        chkDryRun.Name = "chkDryRun";
        chkDryRun.Size = new Size(67, 19);
        chkDryRun.TabIndex = 1;
        chkDryRun.Text = "Dry Run";
        chkDryRun.UseVisualStyleBackColor = true;
        // 
        // chkTraverseSubfolders
        // 
        chkTraverseSubfolders.Anchor = AnchorStyles.Left;
        chkTraverseSubfolders.AutoSize = true;
        chkTraverseSubfolders.Checked = true;
        chkTraverseSubfolders.CheckState = CheckState.Checked;
        chkTraverseSubfolders.Location = new Point(161, 3);
        chkTraverseSubfolders.Name = "chkTraverseSubfolders";
        chkTraverseSubfolders.Size = new Size(142, 19);
        chkTraverseSubfolders.TabIndex = 2;
        chkTraverseSubfolders.Text = "Traverse All Subfolders";
        chkTraverseSubfolders.UseVisualStyleBackColor = true;
        chkTraverseSubfolders.CheckedChanged += chkTraverseSubfolders_CheckedChanged;
        //
        // chkMaintainFolderStructure
        //
        chkMaintainFolderStructure.Anchor = AnchorStyles.Left;
        chkMaintainFolderStructure.AutoSize = true;
        chkMaintainFolderStructure.Location = new Point(309, 3);
        chkMaintainFolderStructure.Name = "chkMaintainFolderStructure";
        chkMaintainFolderStructure.Size = new Size(163, 19);
        chkMaintainFolderStructure.TabIndex = 3;
        chkMaintainFolderStructure.Text = "Maintain Folder Structure";
        chkMaintainFolderStructure.UseVisualStyleBackColor = true;
        //
        // progressOverall
        // 
        progressOverall.Dock = DockStyle.Fill;
        progressOverall.Location = new Point(15, 309);
        progressOverall.Name = "progressOverall";
        progressOverall.Size = new Size(954, 26);
        progressOverall.Style = ProgressBarStyle.Continuous;
        progressOverall.TabIndex = 12;
        // 
        // lblProgressPercent
        // 
        lblProgressPercent.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        lblProgressPercent.Dock = DockStyle.Fill;
        lblProgressPercent.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        lblProgressPercent.Location = new Point(975, 309);
        lblProgressPercent.Name = "lblProgressPercent";
        lblProgressPercent.Size = new Size(94, 26);
        lblProgressPercent.TabIndex = 13;
        lblProgressPercent.Text = "0%";
        lblProgressPercent.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // lblCurrent
        // 
        lblCurrent.AutoEllipsis = true;
        lblCurrent.Dock = DockStyle.Fill;
        lblCurrent.Location = new Point(15, 341);
        lblCurrent.Name = "lblCurrent";
        lblCurrent.Size = new Size(1054, 28);
        lblCurrent.TabIndex = 14;
        lblCurrent.Text = "Current step: idle";
        lblCurrent.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // lblSummary
        // 
        lblSummary.AutoEllipsis = true;
        lblSummary.Dock = DockStyle.Fill;
        lblSummary.Location = new Point(15, 281);
        lblSummary.Name = "lblSummary";
        lblSummary.Size = new Size(1054, 28);
        lblSummary.TabIndex = 15;
        lblSummary.Text = "Directory: not started   Files found: not started";
        lblSummary.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // grpResults
        // 
        grpResults.Controls.Add(layoutResults);
        grpResults.Dock = DockStyle.Fill;
        grpResults.Location = new Point(15, 369);
        grpResults.Name = "grpResults";
        grpResults.Size = new Size(1054, 66);
        grpResults.TabIndex = 16;
        grpResults.TabStop = false;
        grpResults.Text = "Results";
        // 
        // layoutResults
        // 
        layoutResults.ColumnCount = 5;
        layoutResults.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        layoutResults.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        layoutResults.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        layoutResults.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        layoutResults.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        layoutResults.Controls.Add(lblResultsTotalCaption, 0, 0);
        layoutResults.Controls.Add(lblResultsSuccessCaption, 1, 0);
        layoutResults.Controls.Add(lblResultsSkippedCaption, 2, 0);
        layoutResults.Controls.Add(lblResultsFailedCaption, 3, 0);
        layoutResults.Controls.Add(lblResultsRuntimeCaption, 4, 0);
        layoutResults.Controls.Add(lblResultsTotalValue, 0, 1);
        layoutResults.Controls.Add(lblResultsSuccessValue, 1, 1);
        layoutResults.Controls.Add(lblResultsSkippedValue, 2, 1);
        layoutResults.Controls.Add(lblResultsFailedValue, 3, 1);
        layoutResults.Controls.Add(lblResultsRuntimeValue, 4, 1);
        layoutResults.Dock = DockStyle.Fill;
        layoutResults.Location = new Point(3, 19);
        layoutResults.Name = "layoutResults";
        layoutResults.RowCount = 2;
        layoutResults.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));
        layoutResults.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));
        layoutResults.Size = new Size(1048, 44);
        layoutResults.TabIndex = 0;
        // 
        // lblResultsTotalCaption
        // 
        lblResultsTotalCaption.Anchor = AnchorStyles.None;
        lblResultsTotalCaption.AutoSize = true;
        lblResultsTotalCaption.Location = new Point(84, 2);
        lblResultsTotalCaption.Name = "lblResultsTotalCaption";
        lblResultsTotalCaption.Size = new Size(37, 15);
        lblResultsTotalCaption.TabIndex = 0;
        lblResultsTotalCaption.Text = "Total";
        // 
        // lblResultsSuccessCaption
        // 
        lblResultsSuccessCaption.Anchor = AnchorStyles.None;
        lblResultsSuccessCaption.AutoSize = true;
        lblResultsSuccessCaption.Location = new Point(287, 2);
        lblResultsSuccessCaption.Name = "lblResultsSuccessCaption";
        lblResultsSuccessCaption.Size = new Size(50, 15);
        lblResultsSuccessCaption.TabIndex = 1;
        lblResultsSuccessCaption.Text = "Success";
        // 
        // lblResultsSkippedCaption
        // 
        lblResultsSkippedCaption.Anchor = AnchorStyles.None;
        lblResultsSkippedCaption.AutoSize = true;
        lblResultsSkippedCaption.Location = new Point(498, 2);
        lblResultsSkippedCaption.Name = "lblResultsSkippedCaption";
        lblResultsSkippedCaption.Size = new Size(51, 15);
        lblResultsSkippedCaption.TabIndex = 2;
        lblResultsSkippedCaption.Text = "Skipped";
        // 
        // lblResultsFailedCaption
        // 
        lblResultsFailedCaption.Anchor = AnchorStyles.None;
        lblResultsFailedCaption.AutoSize = true;
        lblResultsFailedCaption.Location = new Point(715, 2);
        lblResultsFailedCaption.Name = "lblResultsFailedCaption";
        lblResultsFailedCaption.Size = new Size(39, 15);
        lblResultsFailedCaption.TabIndex = 3;
        lblResultsFailedCaption.Text = "Failed";
        // 
        // lblResultsRuntimeCaption
        // 
        lblResultsRuntimeCaption.Anchor = AnchorStyles.None;
        lblResultsRuntimeCaption.AutoSize = true;
        lblResultsRuntimeCaption.Location = new Point(917, 2);
        lblResultsRuntimeCaption.Name = "lblResultsRuntimeCaption";
        lblResultsRuntimeCaption.Size = new Size(51, 15);
        lblResultsRuntimeCaption.TabIndex = 4;
        lblResultsRuntimeCaption.Text = "Runtime";
        // 
        // lblResultsTotalValue
        // 
        lblResultsTotalValue.Anchor = AnchorStyles.None;
        lblResultsTotalValue.AutoSize = true;
        lblResultsTotalValue.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        lblResultsTotalValue.Location = new Point(99, 21);
        lblResultsTotalValue.Name = "lblResultsTotalValue";
        lblResultsTotalValue.Size = new Size(17, 20);
        lblResultsTotalValue.TabIndex = 5;
        lblResultsTotalValue.Text = "0";
        // 
        // lblResultsSuccessValue
        // 
        lblResultsSuccessValue.Anchor = AnchorStyles.None;
        lblResultsSuccessValue.AutoSize = true;
        lblResultsSuccessValue.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        lblResultsSuccessValue.Location = new Point(304, 21);
        lblResultsSuccessValue.Name = "lblResultsSuccessValue";
        lblResultsSuccessValue.Size = new Size(17, 20);
        lblResultsSuccessValue.TabIndex = 6;
        lblResultsSuccessValue.Text = "0";
        // 
        // lblResultsSkippedValue
        // 
        lblResultsSkippedValue.Anchor = AnchorStyles.None;
        lblResultsSkippedValue.AutoSize = true;
        lblResultsSkippedValue.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        lblResultsSkippedValue.Location = new Point(515, 21);
        lblResultsSkippedValue.Name = "lblResultsSkippedValue";
        lblResultsSkippedValue.Size = new Size(17, 20);
        lblResultsSkippedValue.TabIndex = 7;
        lblResultsSkippedValue.Text = "0";
        // 
        // lblResultsFailedValue
        // 
        lblResultsFailedValue.Anchor = AnchorStyles.None;
        lblResultsFailedValue.AutoSize = true;
        lblResultsFailedValue.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        lblResultsFailedValue.Location = new Point(726, 21);
        lblResultsFailedValue.Name = "lblResultsFailedValue";
        lblResultsFailedValue.Size = new Size(17, 20);
        lblResultsFailedValue.TabIndex = 8;
        lblResultsFailedValue.Text = "0";
        // 
        // lblResultsRuntimeValue
        // 
        lblResultsRuntimeValue.Anchor = AnchorStyles.None;
        lblResultsRuntimeValue.AutoSize = true;
        lblResultsRuntimeValue.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        lblResultsRuntimeValue.Location = new Point(927, 21);
        lblResultsRuntimeValue.Name = "lblResultsRuntimeValue";
        lblResultsRuntimeValue.Size = new Size(31, 20);
        lblResultsRuntimeValue.TabIndex = 9;
        lblResultsRuntimeValue.Text = "0:0";
        // 
        // lstLog
        // 
        lstLog.Dock = DockStyle.Fill;
        lstLog.DrawMode = DrawMode.OwnerDrawFixed;
        lstLog.FormattingEnabled = true;
        lstLog.HorizontalScrollbar = true;
        lstLog.ItemHeight = 15;
        lstLog.Location = new Point(15, 441);
        lstLog.Name = "lstLog";
        lstLog.Size = new Size(1054, 205);
        lstLog.TabIndex = 17;
        lstLog.DoubleClick += lstLog_DoubleClick;
        lstLog.DrawItem += lstLog_DrawItem;
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1084, 731);
        Controls.Add(layoutRoot);
        MinimumSize = new Size(960, 670);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Media MKV Converter";
        layoutRoot.ResumeLayout(false);
        layoutRoot.PerformLayout();
        panelTopActions.ResumeLayout(false);
        panelTopActions.PerformLayout();
        panelAdvanced.ResumeLayout(false);
        layoutAdvanced.ResumeLayout(false);
        layoutAdvanced.PerformLayout();
        grpResults.ResumeLayout(false);
        layoutResults.ResumeLayout(false);
        layoutResults.PerformLayout();
        ResumeLayout(false);
    }
}
