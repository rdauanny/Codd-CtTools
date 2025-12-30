using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CoddCtTools
{
    public partial class MainForm : Form
    {
        private PlantScadaConnector? connector;
        private DataGridView? tagsGridView;
        private Panel? loginPanel;
        private Panel? tagsPanel;
        private TextBox? txtCtApiPath;
        private TextBox? txtServer;
        private TextBox? txtUsername;
        private TextBox? txtPassword;
        private Button? btnConnect;
        private Button? btnDisconnect;
        private Button? btnTestConnection;
        private Label? lblStatus;
        private TextBox? txtTagSearch;
        private CancellationTokenSource? cancellationTokenSource;
        private TagSimulator? tagSimulator;
        private System.Timers.Timer? simulationWriteTimer;
        private string? debugLogPath;
        private object logLock = new object();

        public MainForm()
        {
            tagSimulator = new TagSimulator();
            
            // Inicializar arquivo de log de debug
            string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }
            debugLogPath = Path.Combine(logDir, $"DebugLog_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            WriteDebugLog("=== Application Started ===");
            WriteDebugLog($"Debug log file: {debugLogPath}");
            
            InitializeComponent();
        }

        /// <summary>
        /// Escreve uma mensagem no arquivo de log de debug
        /// </summary>
        private void WriteDebugLog(string message)
        {
            try
            {
                lock (logLock)
                {
                    if (!string.IsNullOrEmpty(debugLogPath))
                    {
                        string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
                        File.AppendAllText(debugLogPath, logMessage + Environment.NewLine);
                    }
                    
                    // Também escreve no Debug Output
                    System.Diagnostics.Debug.WriteLine(message);
                }
            }
            catch
            {
                // Ignorar erros de escrita no log
            }
        }

        private void InitializeComponent()
        {
            this.Text = "Codd Automation - PlantScada / Power Operation - Tester";
            this.Size = new System.Drawing.Size(1200, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = true;

            // Painel de Login
            loginPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 180,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(10),
                AutoSize = false
            };

            var lblCtApiPath = new Label
            {
                Text = "CTAPI:",
                Location = new System.Drawing.Point(10, 15),
                Size = new System.Drawing.Size(80, 23),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };

            txtCtApiPath = new TextBox
            {
                Location = new System.Drawing.Point(100, 12),
                Size = new System.Drawing.Size(400, 23),
                Text = PlantScadaConnector.GetCtApiPath()
            };

            var btnBrowsePath = new Button
            {
                Text = "Browse...",
                Location = new System.Drawing.Point(510, 11),
                Size = new System.Drawing.Size(80, 25)
            };
            btnBrowsePath.Click += (s, e) =>
            {
                using (var folderDialog = new FolderBrowserDialog())
                {
                    folderDialog.Description = "Select the folder where the CTAPI DLLs are located (usually the PlantScada Bin folder)";
                    folderDialog.SelectedPath = txtCtApiPath?.Text ?? "";
                    if (folderDialog.ShowDialog() == DialogResult.OK)
                    {
                        if (txtCtApiPath != null)
                        {
                            txtCtApiPath.Text = folderDialog.SelectedPath;
                            PlantScadaConnector.SetCtApiPath(folderDialog.SelectedPath);
                        }
                    }
                }
            };

            var lblServer = new Label
            {
                Text = "Server:",
                Location = new System.Drawing.Point(10, 45),
                Size = new System.Drawing.Size(80, 23),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };

            txtServer = new TextBox
            {
                Location = new System.Drawing.Point(100, 42),
                Size = new System.Drawing.Size(300, 23),
                Text = "localhost"
            };

            var lblUsername = new Label
            {
                Text = "Username:",
                Location = new System.Drawing.Point(10, 75),
                Size = new System.Drawing.Size(80, 23),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };

            txtUsername = new TextBox
            {
                Location = new System.Drawing.Point(100, 72),
                Size = new System.Drawing.Size(300, 23)
            };

            var lblPassword = new Label
            {
                Text = "Password:",
                Location = new System.Drawing.Point(10, 105),
                Size = new System.Drawing.Size(80, 23),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };

            txtPassword = new TextBox
            {
                Location = new System.Drawing.Point(100, 102),
                Size = new System.Drawing.Size(300, 23),
                PasswordChar = '*'
            };

            btnConnect = new Button
            {
                Text = "Connect",
                Location = new System.Drawing.Point(420, 42),
                Size = new System.Drawing.Size(100, 30),
                UseVisualStyleBackColor = true
            };
            btnConnect.Click += BtnConnect_Click;

            btnDisconnect = new Button
            {
                Text = "Disconnect",
                Location = new System.Drawing.Point(420, 78),
                Size = new System.Drawing.Size(100, 30),
                Enabled = false,
                UseVisualStyleBackColor = true
            };
            btnDisconnect.Click += BtnDisconnect_Click;

            btnTestConnection = new Button
            {
                Text = "Test Connection",
                Location = new System.Drawing.Point(530, 42),
                Size = new System.Drawing.Size(120, 30),
                UseVisualStyleBackColor = true
            };
            btnTestConnection.Click += BtnTestConnection_Click;

            lblStatus = new Label
            {
                Text = "Status: Disconnected",
                Location = new System.Drawing.Point(10, 135),
                Size = new System.Drawing.Size(500, 23),
                ForeColor = System.Drawing.Color.Red
            };

            loginPanel.Controls.AddRange(new Control[] {
                lblCtApiPath, txtCtApiPath, btnBrowsePath,
                lblServer, txtServer,
                lblUsername, txtUsername,
                lblPassword, txtPassword,
                btnConnect, btnDisconnect, btnTestConnection,
                lblStatus
            });
            
            // Atualizar caminho quando o texto mudar (carrega DLL imediatamente)
            txtCtApiPath.TextChanged += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(txtCtApiPath.Text))
                {
                    PlantScadaConnector.SetCtApiPath(txtCtApiPath.Text);
                }
            };
            
            // Garantir que a DLL seja carregada ao iniciar
            PlantScadaConnector.SetCtApiPath(PlantScadaConnector.GetCtApiPath());

            // Painel de Tags
            tagsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            var lblTags = new Label
            {
                Text = "Tags (Real Time):",
                Dock = DockStyle.Top,
                Height = 30,
                Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold)
            };

            // Painel para busca
            var searchPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 35,
                Padding = new Padding(5)
            };

            var lblSearch = new Label
            {
                Text = "Search:",
                Location = new System.Drawing.Point(5, 8),
                Size = new System.Drawing.Size(50, 23),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };

            txtTagSearch = new TextBox
            {
                Location = new System.Drawing.Point(60, 5),
                Size = new System.Drawing.Size(600, 23),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            txtTagSearch.TextChanged += TxtTagSearch_TextChanged;

            searchPanel.Controls.Add(lblSearch);
            searchPanel.Controls.Add(txtTagSearch);

            tagsGridView = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true
            };

            tagsGridView.Columns.Add("TagName", "Tag Name");
            tagsGridView.Columns.Add("Description", "Description");
            tagsGridView.Columns.Add("Value", "Value");
            tagsGridView.Columns.Add("Quality", "Quality");
            tagsGridView.Columns.Add("Timestamp", "Date/Time");
            
            // Adicionar coluna de botão Simulate
            var simulateColumn = new DataGridViewButtonColumn
            {
                Name = "Simulate",
                HeaderText = "Simulate",
                Text = "Simulate",
                UseColumnTextForButtonValue = true,
                Width = 80
            };
            tagsGridView.Columns.Add(simulateColumn);
            
            // Adicionar coluna de botão Set
            var setColumn = new DataGridViewButtonColumn
            {
                Name = "Set",
                HeaderText = "Set",
                Text = "Set",
                UseColumnTextForButtonValue = true,
                Width = 60
            };
            tagsGridView.Columns.Add(setColumn);
            
            // Adicionar coluna de botão Reset
            var resetColumn = new DataGridViewButtonColumn
            {
                Name = "Reset",
                HeaderText = "Reset",
                Text = "Reset",
                UseColumnTextForButtonValue = true,
                Width = 60
            };
            tagsGridView.Columns.Add(resetColumn);
            
            tagsGridView.Columns["TagName"].FillWeight = 20;
            tagsGridView.Columns["Description"].FillWeight = 20;
            tagsGridView.Columns["Value"].FillWeight = 12;
            tagsGridView.Columns["Quality"].FillWeight = 12;
            tagsGridView.Columns["Timestamp"].FillWeight = 12;
            tagsGridView.Columns["Simulate"].FillWeight = 8;
            tagsGridView.Columns["Set"].FillWeight = 6;
            tagsGridView.Columns["Reset"].FillWeight = 6;
            
            // Handler para cliques em botões
            tagsGridView.CellClick += TagsGridView_CellClick;

            // Adicionar menu de contexto (botão direito)
            var contextMenu = new ContextMenuStrip();
            var writeMenuItem = new ToolStripMenuItem("Write Value...");
            writeMenuItem.Click += WriteTagMenuItem_Click;
            contextMenu.Items.Add(writeMenuItem);
            
            contextMenu.Items.Add(new ToolStripSeparator());
            
            var simulateMenuItem = new ToolStripMenuItem("Simulate");
            simulateMenuItem.Click += SimulateMenuItem_Click;
            contextMenu.Items.Add(simulateMenuItem);
            
            var setMenuItem = new ToolStripMenuItem("Set");
            setMenuItem.Click += SetMenuItem_Click;
            contextMenu.Items.Add(setMenuItem);
            
            var resetMenuItem = new ToolStripMenuItem("Reset");
            resetMenuItem.Click += ResetMenuItem_Click;
            contextMenu.Items.Add(resetMenuItem);
            
            tagsGridView.ContextMenuStrip = contextMenu;

            tagsPanel.Controls.Add(tagsGridView);
            tagsPanel.Controls.Add(searchPanel);
            tagsPanel.Controls.Add(lblTags);

            this.Controls.Add(tagsPanel);
            this.Controls.Add(loginPanel);
        }

        /// <summary>
        /// Atualiza os valores das tags na grid (método auxiliar para evitar duplicação de código)
        /// </summary>
        private void UpdateTagValues(Dictionary<string, PlantScadaConnector.TagValue> tagValues)
        {
            if (tagsGridView == null)
            {
                WriteDebugLog("UpdateTagValues: tagsGridView is null");
                return;
            }

            if (tagValues == null || tagValues.Count == 0)
            {
                WriteDebugLog("UpdateTagValues: tagValues is null or empty");
                return;
            }

            int updatedCount = 0;
            int notFoundCount = 0;

            try
            {
                foreach (var kvp in tagValues)
                {
                    try
                    {
                        var row = tagsGridView.Rows
                            .Cast<DataGridViewRow>()
                            .FirstOrDefault(r => r.Cells["TagName"].Value?.ToString() == kvp.Key);

                        if (row != null)
                        {
                            string tagName = kvp.Key;
                            
                            // Verificar se a tag está sendo simulada
                            if (tagSimulator != null && tagSimulator.IsSimulated(tagName))
                            {
                                // Usar valor simulado
                                try
                                {
                                    double simulatedValue = tagSimulator.GenerateSimulatedValue(tagName);
                                    row.Cells["Value"].Value = simulatedValue.ToString();
                                    row.Cells["Quality"].Value = "Simulated";
                                    row.Cells["Timestamp"].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                    
                                    // Atualizar o texto do botão para "Parar"
                                    if (row.Cells["Simulate"] is DataGridViewButtonCell btnCell)
                                    {
                                        btnCell.Value = "Stop";
                                    }
                                    updatedCount++;
                                }
                                catch (Exception ex)
                                {
                                    WriteDebugLog($"Error updating simulated tag {tagName}: {ex.Message}");
                                    // Em caso de erro, usar valor real
                                    row.Cells["Value"].Value = kvp.Value.Value?.ToString() ?? "---";
                                    row.Cells["Quality"].Value = kvp.Value.Quality ?? "---";
                                    row.Cells["Timestamp"].Value = kvp.Value.Timestamp?.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? "---";
                                    updatedCount++;
                                }
                            }
                            else
                            {
                                // Usar valor real da tag
                                string valueStr = kvp.Value.Value?.ToString() ?? "---";
                                string qualityStr = kvp.Value.Quality ?? "---";
                                string timestampStr = kvp.Value.Timestamp?.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? "---";
                                
                                row.Cells["Value"].Value = valueStr;
                                row.Cells["Quality"].Value = qualityStr;
                                row.Cells["Timestamp"].Value = timestampStr;
                                
                                // Atualizar o texto do botão para "Simular"
                                if (row.Cells["Simulate"] is DataGridViewButtonCell btnCell)
                                {
                                    btnCell.Value = "Simulate";
                                }
                                updatedCount++;
                            }
                        }
                        else
                        {
                            notFoundCount++;
                            WriteDebugLog($"Tag '{kvp.Key}' not found in grid");
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteDebugLog($"Error processing tag {kvp.Key}: {ex.Message}");
                        // Ignorar erros individuais e continuar com outras tags
                        continue;
                    }
                }
                
                WriteDebugLog($"UpdateTagValues completed: {updatedCount} updated, {notFoundCount} not found");
            }
            catch (Exception ex)
            {
                WriteDebugLog($"Fatal error in UpdateTagValues: {ex.Message}");
            }
        }

        private void TxtTagSearch_TextChanged(object? sender, EventArgs e)
        {
            if (tagsGridView == null) return;

            string searchText = txtTagSearch?.Text?.ToLower() ?? "";
            
            // Filtrar as linhas do DataGridView e limpar seleção de linhas não visíveis
            foreach (DataGridViewRow row in tagsGridView.Rows)
            {
                bool shouldBeVisible;
                
                if (string.IsNullOrEmpty(searchText))
                {
                    shouldBeVisible = true;
                }
                else
                {
                    // Buscar tanto no TagName quanto na Description
                    string tagName = "";
                    string description = "";
                    
                    if (row.Cells["TagName"]?.Value != null)
                    {
                        tagName = row.Cells["TagName"].Value.ToString()?.ToLower() ?? "";
                    }
                    
                    if (row.Cells["Description"]?.Value != null)
                    {
                        description = row.Cells["Description"].Value.ToString()?.ToLower() ?? "";
                    }
                    
                    // A linha será visível se o texto de busca estiver no nome da tag OU na descrição
                    shouldBeVisible = tagName.Contains(searchText) || description.Contains(searchText);
                }
                
                // Se a linha está selecionada mas não deve estar visível, desmarcar
                if (row.Selected && !shouldBeVisible)
                {
                    row.Selected = false;
                }
                
                row.Visible = shouldBeVisible;
            }
        }

        private async void BtnConnect_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtServer?.Text) ||
                string.IsNullOrWhiteSpace(txtUsername?.Text) ||
                string.IsNullOrWhiteSpace(txtPassword?.Text))
            {
                MessageBox.Show("Please fill in all connection fields.", "Warning", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                btnConnect!.Enabled = false;
                lblStatus!.Text = "Status: Connecting...";
                lblStatus.ForeColor = System.Drawing.Color.Orange;

                // Configurar caminho das DLLs antes de conectar
                // IMPORTANTE: Isso carrega a DLL antes de qualquer uso
                if (!string.IsNullOrWhiteSpace(txtCtApiPath?.Text))
                {
                    PlantScadaConnector.SetCtApiPath(txtCtApiPath.Text);
                }
                else
                {
                    // Garantir que a DLL seja carregada mesmo se o caminho não foi alterado
                    PlantScadaConnector.SetCtApiPath(PlantScadaConnector.GetCtApiPath());
                }

                // Configurar log
                string logPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    $"CtApi_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                PlantScadaConnector.SetLogFile(logPath);

                connector = new PlantScadaConnector();

                bool connected = await Task.Run(() =>
                    connector.Connect(
                        txtServer.Text,
                        txtUsername.Text,
                        txtPassword?.Text ?? ""));

                if (connected)
                {
                    WriteDebugLog("Connection successful, setting up UI...");
                    lblStatus.Text = "Status: Connected";
                    lblStatus.ForeColor = System.Drawing.Color.Green;
                    btnConnect.Enabled = false;
                    btnDisconnect!.Enabled = true;

                    // Carregar tags com informações (nome e descrição)
                    WriteDebugLog("Loading tag information...");
                    var tagInfos = await Task.Run(() => 
                    {
                        try
                        {
                            WriteDebugLog("Calling GetAllTagInfos...");
                            var result = connector.GetAllTagInfos();
                            WriteDebugLog($"GetAllTagInfos returned {result?.Count ?? 0} tags");
                            return result;
                        }
                        catch (Exception ex)
                        {
                            WriteDebugLog($"Error in GetAllTagInfos: {ex.Message}");
                            WriteDebugLog($"Stack trace: {ex.StackTrace}");
                            throw;
                        }
                    });

                    if (tagInfos != null && tagInfos.Count > 0)
                    {
                        WriteDebugLog($"Found {tagInfos.Count} tags, adding to grid...");
                        
                        // Limpar grid
                        tagsGridView!.Invoke((MethodInvoker)delegate
                        {
                            tagsGridView.Rows.Clear();
                        });

                        // Adicionar tags ao grid
                        int addedCount = 0;
                        foreach (var tagInfo in tagInfos)
                        {
                            tagsGridView!.Invoke((MethodInvoker)delegate
                            {
                                tagsGridView.Rows.Add(tagInfo.Name, tagInfo.Description ?? "", "---", "---", "---", "Simulate", "Set", "Reset");
                            });
                            addedCount++;
                        }
                        WriteDebugLog($"Added {addedCount} tags to grid");

                    // Iniciar atualização em tempo real
                    WriteDebugLog("Preparing to start real-time updates...");
                    cancellationTokenSource = new CancellationTokenSource();
                    WriteDebugLog("Starting real-time updates...");
                    WriteDebugLog($"Debug log file location: {debugLogPath}");
                    WriteDebugLog($"Connector IsConnected: {connector.IsConnected}");
                    WriteDebugLog($"tagsGridView is null: {tagsGridView == null}");
                    
                    Task.Run(async () => 
                    {
                        try
                        {
                            WriteDebugLog("Task.Run started for StartRealTimeUpdates");
                            await StartRealTimeUpdates(cancellationTokenSource.Token);
                        }
                        catch (Exception ex)
                        {
                            WriteDebugLog($"Exception in Task.Run for StartRealTimeUpdates: {ex.Message}");
                            WriteDebugLog($"Stack trace: {ex.StackTrace}");
                        }
                    });
                    WriteDebugLog("StartRealTimeUpdates task started");
                    
                    // Iniciar timer para escrever tags simuladas a cada 5 segundos
                    WriteDebugLog("Starting simulation write timer...");
                    StartSimulationWriteTimer();
                    WriteDebugLog("Setup complete!");
                    }
                    else
                    {
                        WriteDebugLog("No tags found in the project");
                        MessageBox.Show("No tags found in the project.", "Information", 
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                else
                {
                    lblStatus.Text = "Status: Connection failed";
                    lblStatus.ForeColor = System.Drawing.Color.Red;
                    btnConnect.Enabled = true;
                    MessageBox.Show($"Failed to connect. Check the log at:\n{logPath}", "Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                lblStatus!.Text = "Status: Error";
                lblStatus.ForeColor = System.Drawing.Color.Red;
                btnConnect!.Enabled = true;
                MessageBox.Show($"Error connecting:\n\n{ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task StartRealTimeUpdates(CancellationToken cancellationToken)
        {
            if (connector == null || tagsGridView == null)
            {
                WriteDebugLog("StartRealTimeUpdates: connector or tagsGridView is null");
                return;
            }

            WriteDebugLog($"StartRealTimeUpdates started. IsConnected: {connector.IsConnected}");

            try
            {
                while (!cancellationToken.IsCancellationRequested && connector.IsConnected)
                {
                    try
                    {
                        WriteDebugLog("Starting tag read cycle...");
                        
                        // Ler valores de todas as tags
                        Dictionary<string, PlantScadaConnector.TagValue>? tagValues = null;
                        
                        try
                        {
                            tagValues = await Task.Run(() =>
                            {
                                try
                                {
                                    WriteDebugLog("Reading tag values from grid...");
                                    
                                    // Ler TODAS as tags da grid, independente do filtro de visibilidade
                                    var tagsInGrid = new List<string>();
                                    tagsGridView?.Invoke((MethodInvoker)delegate
                                    {
                                        foreach (DataGridViewRow row in tagsGridView.Rows)
                                        {
                                            // Ler todas as tags, independente se estão visíveis ou não
                                            if (row.Cells["TagName"]?.Value != null)
                                            {
                                                string tagName = row.Cells["TagName"].Value.ToString() ?? "";
                                                if (!string.IsNullOrEmpty(tagName))
                                                {
                                                    tagsInGrid.Add(tagName);
                                                }
                                            }
                                        }
                                    });
                                    
                                    WriteDebugLog($"Found {tagsInGrid.Count} tags in grid (all tags, ignoring filter), reading values...");
                                    
                                    // Ler valores apenas das tags que estão na grid
                                    var result = new Dictionary<string, PlantScadaConnector.TagValue>();
                                    int readCount = 0;
                                    int errorCount = 0;
                                    
                                    foreach (var tagName in tagsInGrid)
                                    {
                                        try
                                        {
                                            var tagValue = connector.ReadTag(tagName);
                                            result[tagName] = tagValue;
                                            readCount++;
                                            
                                            // Log a cada 500 tags para não sobrecarregar
                                            if (readCount % 500 == 0)
                                            {
                                                WriteDebugLog($"Read {readCount}/{tagsInGrid.Count} tags...");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            errorCount++;
                                            result[tagName] = new PlantScadaConnector.TagValue
                                            {
                                                Value = null,
                                                Quality = $"Error: {ex.Message}",
                                                Timestamp = DateTime.Now
                                            };
                                            
                                            // Log apenas os primeiros erros
                                            if (errorCount <= 5)
                                            {
                                                WriteDebugLog($"Error reading tag '{tagName}': {ex.Message}");
                                            }
                                        }
                                    }
                                    
                                    WriteDebugLog($"ReadAllTags completed: {readCount} successful, {errorCount} errors, total: {result.Count} tags");
                                    return result;
                                }
                                catch (Exception ex)
                                {
                                    WriteDebugLog($"Error reading tags: {ex.Message}");
                                    WriteDebugLog($"Stack trace: {ex.StackTrace}");
                                    return null;
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            WriteDebugLog($"Exception in Task.Run for ReadAllTags: {ex.Message}");
                            WriteDebugLog($"Stack trace: {ex.StackTrace}");
                        }

                        if (tagValues != null && tagValues.Count > 0)
                        {
                            WriteDebugLog($"Updating {tagValues.Count} tag values in grid");
                            
                            if (tagsGridView.InvokeRequired)
                            {
                                tagsGridView.Invoke((MethodInvoker)delegate
                                {
                                    try
                                    {
                                        WriteDebugLog("Invoking UpdateTagValues...");
                                        UpdateTagValues(tagValues);
                                        WriteDebugLog("Tag values updated successfully via Invoke");
                                    }
                                    catch (Exception ex)
                                    {
                                        WriteDebugLog($"Error updating tag values: {ex.Message}");
                                        WriteDebugLog($"Stack trace: {ex.StackTrace}");
                                    }
                                });
                            }
                            else
                            {
                                try
                                {
                                    WriteDebugLog("Calling UpdateTagValues directly...");
                                    UpdateTagValues(tagValues);
                                    WriteDebugLog("Tag values updated successfully directly");
                                }
                                catch (Exception ex)
                                {
                                    WriteDebugLog($"Error updating tag values: {ex.Message}");
                                    WriteDebugLog($"Stack trace: {ex.StackTrace}");
                                }
                            }
                        }
                        else
                        {
                            WriteDebugLog($"No tag values to update (tagValues is null: {tagValues == null}, count: {tagValues?.Count ?? 0})");
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteDebugLog($"Error in update loop: {ex.Message}");
                        WriteDebugLog($"Stack trace: {ex.StackTrace}");
                        // Continuar mesmo em caso de erro
                    }

                    // Aguardar antes da próxima atualização (100ms = 10 atualizações por segundo)
                    await Task.Delay(100, cancellationToken);
                }
                
                WriteDebugLog("StartRealTimeUpdates loop ended");
            }
            catch (OperationCanceledException)
            {
                // Cancelamento normal, não fazer nada
                WriteDebugLog("Real-time updates cancelled");
            }
            catch (Exception ex)
            {
                WriteDebugLog($"Fatal error in StartRealTimeUpdates: {ex.Message}");
                WriteDebugLog($"Stack trace: {ex.StackTrace}");
                if (!cancellationToken.IsCancellationRequested)
                {
                    tagsGridView?.Invoke((MethodInvoker)delegate
                    {
                        MessageBox.Show($"Error during tag update:\n\n{ex.Message}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    });
                }
            }
        }

        private void BtnDisconnect_Click(object? sender, EventArgs e)
        {
            try
            {
                cancellationTokenSource?.Cancel();
                StopSimulationWriteTimer();
                connector?.Disconnect();
                tagSimulator?.ClearAll();

                btnConnect!.Enabled = true;
                btnDisconnect!.Enabled = false;
                lblStatus!.Text = "Status: Disconnected";
                lblStatus.ForeColor = System.Drawing.Color.Red;

                tagsGridView!.Rows.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error disconnecting: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        /// <summary>
        /// Inicia o timer para escrever tags simuladas no servidor a cada 5 segundos
        /// </summary>
        private void StartSimulationWriteTimer()
        {
            StopSimulationWriteTimer();
            
            simulationWriteTimer = new System.Timers.Timer(1000); // Verifica a cada 1 segundo
            simulationWriteTimer.Elapsed += SimulationWriteTimer_Elapsed;
            simulationWriteTimer.AutoReset = true;
            simulationWriteTimer.Start();
        }
        
        /// <summary>
        /// Para o timer de escrita de simulação
        /// </summary>
        private void StopSimulationWriteTimer()
        {
            if (simulationWriteTimer != null)
            {
                simulationWriteTimer.Stop();
                simulationWriteTimer.Elapsed -= SimulationWriteTimer_Elapsed;
                simulationWriteTimer.Dispose();
                simulationWriteTimer = null;
            }
        }
        
        /// <summary>
        /// Evento do timer: escreve valores simulados nas tags a cada 5 segundos
        /// </summary>
        private void SimulationWriteTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (connector == null || !connector.IsConnected || tagSimulator == null)
                return;
            
            try
            {
                var simulatedTags = tagSimulator.GetAllSimulatedTags();
                
                foreach (var kvp in simulatedTags)
                {
                    string tagName = kvp.Key;
                    var tagInfo = kvp.Value;
                    
                    // Verificar se deve escrever (passou 5 segundos)
                    if (tagSimulator.ShouldWrite(tagName, 5))
                    {
                        try
                        {
                            // Gerar valor simulado
                            double simulatedValue = tagSimulator.GenerateSimulatedValue(tagName);
                            
                            // Escrever no servidor
                            connector.WriteTag(tagName, simulatedValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                            
                            // Atualizar último horário de escrita
                            tagSimulator.UpdateLastWriteTime(tagName);
                        }
                        catch (Exception ex)
                        {
                            // Log erro mas continua com outras tags
                            WriteDebugLog($"Error writing simulated tag '{tagName}': {ex.Message}");
                        }
                    }
                }
            }
            catch
            {
                // Ignorar erros no timer
            }
        }

        private async void BtnTestConnection_Click(object? sender, EventArgs e)
        {
            try
            {
                btnTestConnection!.Enabled = false;

                // Configurar log
                string logPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    $"CtApi_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                PlantScadaConnector.SetLogFile(logPath);

                lblStatus!.Text = "Status: Testing connection...";
                lblStatus.ForeColor = System.Drawing.Color.Orange;

                var testConnector = new PlantScadaConnector();
                var testResult = await Task.Run(() =>
                    testConnector.TestConnection(
                        txtServer!.Text,
                        txtUsername!.Text,
                        txtPassword?.Text ?? ""));

                if (testResult.Success)
                {
                    lblStatus.Text = $"Status: Teste OK - {testResult.Configuration}";
                    lblStatus.ForeColor = System.Drawing.Color.Green;
                    MessageBox.Show(
                        $"✓ Connection test successful!\n\nConfiguration that worked:\n{testResult.Configuration}\n\nHandle: {testResult.Handle}\n\nLog saved at:\n{logPath}",
                        "Connection Test",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    lblStatus.Text = "Status: Test failed";
                    lblStatus.ForeColor = System.Drawing.Color.Red;
                    MessageBox.Show(
                        $"✗ Connection test failed.\n\n{testResult.Message}\n\nLog saved at:\n{logPath}\n\nCheck the log file for more details.",
                        "Connection Test",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                lblStatus!.Text = "Status: Test error";
                lblStatus.ForeColor = System.Drawing.Color.Red;
                MessageBox.Show(
                    $"Error testing connection:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                if (btnTestConnection != null)
                {
                    btnTestConnection.Enabled = true;
                }
            }
        }

        /// <summary>
        /// Obtém apenas as linhas selecionadas que estão visíveis (filtradas)
        /// </summary>
        private List<DataGridViewRow> GetVisibleSelectedRows()
        {
            if (tagsGridView == null)
                return new List<DataGridViewRow>();

            return tagsGridView.SelectedRows
                .Cast<DataGridViewRow>()
                .Where(row => row.Visible)
                .ToList();
        }

        private void WriteTagMenuItem_Click(object? sender, EventArgs e)
        {
            var selectedRows = GetVisibleSelectedRows();
            
            if (selectedRows.Count == 0 || connector == null || !connector.IsConnected)
            {
                MessageBox.Show("Please select at least one tag to write.", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                
                if (selectedRows.Count == 0)
                    return;

                // Obter valor atual da primeira linha selecionada (ou vazio se múltiplas)
                string currentValue = "";
                if (selectedRows.Count == 1)
                {
                    currentValue = selectedRows[0]?.Cells["Value"]?.Value?.ToString() ?? "";
                }

                // Criar diálogo para inserir valor
                using (var inputDialog = new Form())
                {
                    string dialogTitle = selectedRows.Count == 1 
                        ? $"Write value to tag: {selectedRows[0].Cells["TagName"]?.Value?.ToString() ?? ""}"
                        : $"Write value to {selectedRows.Count} tags";
                    
                    inputDialog.Text = dialogTitle;
                    inputDialog.Size = new System.Drawing.Size(400, 150);
                    inputDialog.StartPosition = FormStartPosition.CenterParent;
                    inputDialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                    inputDialog.MaximizeBox = false;
                    inputDialog.MinimizeBox = false;

                    var lblValue = new Label
                    {
                        Text = "New value:",
                        Location = new System.Drawing.Point(10, 20),
                        Size = new System.Drawing.Size(100, 23),
                        TextAlign = System.Drawing.ContentAlignment.MiddleLeft
                    };

                    var txtValue = new TextBox
                    {
                        Location = new System.Drawing.Point(120, 17),
                        Size = new System.Drawing.Size(250, 23),
                        Text = currentValue
                    };

                    var btnOK = new Button
                    {
                        Text = "OK",
                        DialogResult = DialogResult.OK,
                        Location = new System.Drawing.Point(195, 60),
                        Size = new System.Drawing.Size(75, 30)
                    };

                    var btnCancel = new Button
                    {
                        Text = "Cancel",
                        DialogResult = DialogResult.Cancel,
                        Location = new System.Drawing.Point(280, 60),
                        Size = new System.Drawing.Size(90, 30)
                    };

                    inputDialog.Controls.Add(lblValue);
                    inputDialog.Controls.Add(txtValue);
                    inputDialog.Controls.Add(btnOK);
                    inputDialog.Controls.Add(btnCancel);
                    inputDialog.AcceptButton = btnOK;
                    inputDialog.CancelButton = btnCancel;

                    if (inputDialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(txtValue.Text))
                    {
                        int successCount = 0;
                        int errorCount = 0;
                        var errorMessages = new List<string>();

                        // Escrever valor em todas as tags selecionadas
                        foreach (var row in selectedRows)
                        {
                            string tagName = row?.Cells["TagName"]?.Value?.ToString() ?? "";
                            
                            if (string.IsNullOrEmpty(tagName))
                            {
                                errorCount++;
                                continue;
                            }

                            try
                            {
                                connector.WriteTag(tagName, txtValue.Text);
                                successCount++;

                                // Atualizar o valor na grid imediatamente
                                if (row != null)
                                {
                                    row.Cells["Value"].Value = txtValue.Text;
                                    row.Cells["Timestamp"].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                }
                            }
                            catch (Exception ex)
                            {
                                errorCount++;
                                errorMessages.Add($"Tag '{tagName}': {ex.Message}");
                            }
                        }

                        // Mostrar mensagem de resultado
                        if (errorCount == 0)
                        {
                            string message = selectedRows.Count == 1
                                ? $"Value '{txtValue.Text}' written successfully to tag '{selectedRows[0].Cells["TagName"]?.Value?.ToString() ?? ""}'."
                                : $"Value '{txtValue.Text}' written successfully to {successCount} tag(s).";
                            
                            MessageBox.Show(message, "Success",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            string message = $"Written to {successCount} tag(s).\n\n";
                            message += $"Failed to write to {errorCount} tag(s):\n";
                            message += string.Join("\n", errorMessages.Take(5));
                            if (errorMessages.Count > 5)
                            {
                                message += $"\n... and {errorMessages.Count - 5} more error(s).";
                            }
                            
                            MessageBox.Show(message, "Partial Success",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error writing tags:\n\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void TagsGridView_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (tagsGridView == null || e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var row = tagsGridView.Rows[e.RowIndex];
            string tagName = row.Cells["TagName"].Value?.ToString() ?? "";
            string columnName = tagsGridView.Columns[e.ColumnIndex].Name;

            if (string.IsNullOrEmpty(tagName))
            {
                MessageBox.Show("Could not identify the selected tag.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Verificar se foi clicado na coluna Set
            if (columnName == "Set")
            {
                WriteTagValue(tagName, "1", row);
                return;
            }

            // Verificar se foi clicado na coluna Reset
            if (columnName == "Reset")
            {
                WriteTagValue(tagName, "0", row);
                return;
            }

            // Verificar se foi clicado na coluna Simulate
            if (columnName == "Simulate")
            {
                // Verificar se a tag já está sendo simulada
                if (tagSimulator != null && tagSimulator.IsSimulated(tagName))
                {
                    // Parar simulação
                    tagSimulator.RemoveSimulation(tagName);
                    
                    // Atualizar texto do botão
                    if (row.Cells["Simulate"] is DataGridViewButtonCell btnCell)
                    {
                        btnCell.Value = "Simulate";
                    }
                    
                    MessageBox.Show($"Simulation of tag '{tagName}' has been stopped.", "Simulation Stopped",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    // Iniciar simulação - mostrar diálogo
                    var simInfo = tagSimulator?.GetSimulationInfo(tagName);
                    double? currentMin = simInfo?.MinValue;
                    double? currentMax = simInfo?.MaxValue;

                    using (var simulateDialog = new SimulateTagDialog(tagName, currentMin, currentMax))
                    {
                        if (simulateDialog.ShowDialog() == DialogResult.OK && !simulateDialog.Cancelled)
                        {
                            // Configurar simulação
                            tagSimulator?.SetSimulation(tagName, simulateDialog.MinValue, simulateDialog.MaxValue);
                            
                            // Escrever valor imediatamente (não esperar 5 segundos)
                            if (connector != null && connector.IsConnected)
                            {
                                try
                                {
                                    double simulatedValue = tagSimulator!.GenerateSimulatedValue(tagName);
                                    connector.WriteTag(tagName, simulatedValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                                    tagSimulator.UpdateLastWriteTime(tagName);
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"Error writing initial value to tag: {ex.Message}", "Warning",
                                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                }
                            }
                            
                            // Atualizar texto do botão
                            if (row.Cells["Simulate"] is DataGridViewButtonCell btnCell)
                            {
                                btnCell.Value = "Stop";
                            }
                            
                            MessageBox.Show($"Simulation configured for tag '{tagName}':\nMinimum: {simulateDialog.MinValue}\nMaximum: {simulateDialog.MaxValue}\n\nValues will be written to the server every 5 seconds.",
                                "Simulation Started",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Escreve um valor em uma tag e atualiza a grid
        /// </summary>
        private void WriteTagValue(string tagName, string value, DataGridViewRow row)
        {
            if (connector == null || !connector.IsConnected)
            {
                MessageBox.Show("Not connected to server.", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                connector.WriteTag(tagName, value);
                
                // Atualizar o valor na grid imediatamente
                if (row != null)
                {
                    row.Cells["Value"].Value = value;
                    row.Cells["Timestamp"].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error writing tag '{tagName}':\n\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Handler do menu de contexto: Set (escreve "1" em múltiplas tags)
        /// </summary>
        private void SetMenuItem_Click(object? sender, EventArgs e)
        {
            var selectedRows = GetVisibleSelectedRows();
            
            if (selectedRows.Count == 0 || connector == null || !connector.IsConnected)
            {
                MessageBox.Show("Please select at least one tag.", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            WriteMultipleTags(selectedRows, "1", "Set");
        }

        /// <summary>
        /// Handler do menu de contexto: Reset (escreve "0" em múltiplas tags)
        /// </summary>
        private void ResetMenuItem_Click(object? sender, EventArgs e)
        {
            var selectedRows = GetVisibleSelectedRows();
            
            if (selectedRows.Count == 0 || connector == null || !connector.IsConnected)
            {
                MessageBox.Show("Please select at least one tag.", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            WriteMultipleTags(selectedRows, "0", "Reset");
        }

        /// <summary>
        /// Escreve um valor em múltiplas tags
        /// </summary>
        private void WriteMultipleTags(List<DataGridViewRow> rows, string value, string operationName)
        {
            int successCount = 0;
            int errorCount = 0;
            var errorMessages = new List<string>();

            foreach (var row in rows)
            {
                string tagName = row?.Cells["TagName"]?.Value?.ToString() ?? "";
                
                if (string.IsNullOrEmpty(tagName))
                {
                    errorCount++;
                    continue;
                }

                try
                {
                    connector!.WriteTag(tagName, value);
                    successCount++;

                    // Atualizar o valor na grid imediatamente
                    if (row != null)
                    {
                        row.Cells["Value"].Value = value;
                        row.Cells["Timestamp"].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    errorMessages.Add($"Tag '{tagName}': {ex.Message}");
                }
            }

            // Mostrar mensagem de resultado
            if (errorCount == 0)
            {
                string message = rows.Count == 1
                    ? $"{operationName} applied successfully to tag '{rows[0].Cells["TagName"]?.Value?.ToString() ?? ""}'."
                    : $"{operationName} applied successfully to {successCount} tag(s).";
                
                MessageBox.Show(message, "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                string message = $"{operationName} applied to {successCount} tag(s).\n\n";
                message += $"Failed to apply to {errorCount} tag(s):\n";
                message += string.Join("\n", errorMessages.Take(5));
                if (errorMessages.Count > 5)
                {
                    message += $"\n... and {errorMessages.Count - 5} more error(s).";
                }
                
                MessageBox.Show(message, "Partial Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// Handler do menu de contexto: Simulate (inicia/para simulação em múltiplas tags)
        /// </summary>
        private void SimulateMenuItem_Click(object? sender, EventArgs e)
        {
            var selectedRows = GetVisibleSelectedRows();
            
            if (selectedRows.Count == 0 || connector == null || !connector.IsConnected)
            {
                MessageBox.Show("Please select at least one tag.", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var tagNames = selectedRows
                .Select(r => r?.Cells["TagName"]?.Value?.ToString() ?? "")
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();

            if (tagNames.Count == 0)
            {
                MessageBox.Show("Could not identify the selected tags.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Verificar se alguma tag já está sendo simulada
            var simulatedTags = tagNames.Where(t => tagSimulator != null && tagSimulator.IsSimulated(t)).ToList();
            var notSimulatedTags = tagNames.Where(t => tagSimulator == null || !tagSimulator.IsSimulated(t)).ToList();

            // Se todas estão simulando, perguntar se quer parar todas
            if (simulatedTags.Count == tagNames.Count)
            {
                var result = MessageBox.Show(
                    $"All {tagNames.Count} selected tag(s) are currently being simulated.\n\nDo you want to stop simulation for all of them?",
                    "Stop Simulation",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    StopSimulationForTags(simulatedTags, selectedRows);
                }
                return;
            }

            // Se algumas estão simulando e outras não, perguntar o que fazer
            if (simulatedTags.Count > 0 && notSimulatedTags.Count > 0)
            {
                var result = MessageBox.Show(
                    $"{simulatedTags.Count} tag(s) are currently being simulated and {notSimulatedTags.Count} tag(s) are not.\n\n" +
                    "What would you like to do?\n\n" +
                    "Yes: Start simulation for all tags\n" +
                    "No: Stop simulation for tags that are currently simulating\n" +
                    "Cancel: Do nothing",
                    "Mixed Simulation State",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    // Parar as que estão simulando e iniciar todas
                    StopSimulationForTags(simulatedTags, selectedRows);
                    StartSimulationForTags(tagNames, selectedRows);
                }
                else if (result == DialogResult.No)
                {
                    // Apenas parar as que estão simulando
                    StopSimulationForTags(simulatedTags, selectedRows);
                }
                return;
            }

            // Se nenhuma está simulando, iniciar simulação para todas
            StartSimulationForTags(tagNames, selectedRows);
        }

        /// <summary>
        /// Para simulação para múltiplas tags
        /// </summary>
        private void StopSimulationForTags(List<string> tagNames, List<DataGridViewRow> rows)
        {
            int successCount = 0;
            int errorCount = 0;

            foreach (var tagName in tagNames)
            {
                try
                {
                    tagSimulator?.RemoveSimulation(tagName);
                    successCount++;

                    // Atualizar texto do botão
                    var row = rows.FirstOrDefault(r => r?.Cells["TagName"]?.Value?.ToString() == tagName);
                    if (row != null && row.Cells["Simulate"] is DataGridViewButtonCell btnCell)
                    {
                        btnCell.Value = "Simulate";
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    WriteDebugLog($"Error stopping simulation for tag '{tagName}': {ex.Message}");
                }
            }

            string message = tagNames.Count == 1
                ? $"Simulation of tag '{tagNames[0]}' has been stopped."
                : $"Simulation stopped for {successCount} tag(s).";
            
            if (errorCount > 0)
            {
                message += $"\n\nFailed to stop {errorCount} tag(s).";
            }

            MessageBox.Show(message, "Simulation Stopped",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// Inicia simulação para múltiplas tags
        /// </summary>
        private void StartSimulationForTags(List<string> tagNames, List<DataGridViewRow> rows)
        {
            // Mostrar diálogo para configurar valores min/max (usar valores da primeira tag ou padrões)
            string firstTagName = tagNames[0];
            var simInfo = tagSimulator?.GetSimulationInfo(firstTagName);
            double? currentMin = simInfo?.MinValue;
            double? currentMax = simInfo?.MaxValue;

            string dialogTitle = tagNames.Count == 1
                ? $"Simulate Tag: {firstTagName}"
                : $"Simulate {tagNames.Count} Tags";
            
            string description = tagNames.Count == 1
                ? $"Tag: {firstTagName}"
                : $"Configuring simulation for {tagNames.Count} selected tags";

            using (var simulateDialog = new SimulateTagDialog(dialogTitle, description, currentMin, currentMax))
            {
                if (simulateDialog.ShowDialog() == DialogResult.OK && !simulateDialog.Cancelled)
                {
                    int successCount = 0;
                    int errorCount = 0;
                    var errorMessages = new List<string>();

                    // Configurar simulação para todas as tags
                    foreach (var tagName in tagNames)
                    {
                        try
                        {
                            tagSimulator?.SetSimulation(tagName, simulateDialog.MinValue, simulateDialog.MaxValue);

                            // Escrever valor imediatamente
                            if (connector != null && connector.IsConnected)
                            {
                                try
                                {
                                    double simulatedValue = tagSimulator!.GenerateSimulatedValue(tagName);
                                    connector.WriteTag(tagName, simulatedValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                                    tagSimulator.UpdateLastWriteTime(tagName);
                                }
                                catch (Exception ex)
                                {
                                    errorMessages.Add($"Tag '{tagName}' (write error): {ex.Message}");
                                }
                            }

                            // Atualizar texto do botão
                            var row = rows.FirstOrDefault(r => r?.Cells["TagName"]?.Value?.ToString() == tagName);
                            if (row != null && row.Cells["Simulate"] is DataGridViewButtonCell btnCell)
                            {
                                btnCell.Value = "Stop";
                            }

                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            errorCount++;
                            errorMessages.Add($"Tag '{tagName}': {ex.Message}");
                        }
                    }

                    // Mostrar mensagem de resultado
                    string message = tagNames.Count == 1
                        ? $"Simulation configured for tag '{tagNames[0]}':\nMinimum: {simulateDialog.MinValue}\nMaximum: {simulateDialog.MaxValue}\n\nValues will be written to the server every 5 seconds."
                        : $"Simulation configured for {successCount} tag(s):\nMinimum: {simulateDialog.MinValue}\nMaximum: {simulateDialog.MaxValue}\n\nValues will be written to the server every 5 seconds.";

                    if (errorCount > 0)
                    {
                        message += $"\n\nFailed to configure {errorCount} tag(s):\n";
                        message += string.Join("\n", errorMessages.Take(5));
                        if (errorMessages.Count > 5)
                        {
                            message += $"\n... and {errorMessages.Count - 5} more error(s).";
                        }
                    }

                    MessageBox.Show(message, "Simulation Started",
                        MessageBoxButtons.OK, errorCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            WriteDebugLog("=== Application Closing ===");
            if (!string.IsNullOrEmpty(debugLogPath))
            {
                WriteDebugLog($"Debug log saved to: {debugLogPath}");
            }
            
            cancellationTokenSource?.Cancel();
            StopSimulationWriteTimer();
            connector?.Disconnect();
            tagSimulator?.ClearAll();
            base.OnFormClosing(e);
        }
    }
}
