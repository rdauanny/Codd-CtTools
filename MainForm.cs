using System;
using System.Collections.Generic;
using System.Data;
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

        public MainForm()
        {
            tagSimulator = new TagSimulator();
            InitializeComponent();
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
                MultiSelect = false
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
            tagsGridView.ContextMenuStrip = contextMenu;

            tagsPanel.Controls.Add(tagsGridView);
            tagsPanel.Controls.Add(searchPanel);
            tagsPanel.Controls.Add(lblTags);

            this.Controls.Add(tagsPanel);
            this.Controls.Add(loginPanel);
        }

        private void TxtTagSearch_TextChanged(object? sender, EventArgs e)
        {
            if (tagsGridView == null) return;

            string searchText = txtTagSearch?.Text?.ToLower() ?? "";
            
            // Filtrar as linhas do DataGridView
            foreach (DataGridViewRow row in tagsGridView.Rows)
            {
                if (row.Cells.Count > 0 && row.Cells[0].Value != null)
                {
                    string tagName = row.Cells[0].Value.ToString()?.ToLower() ?? "";
                    row.Visible = string.IsNullOrEmpty(searchText) || tagName.Contains(searchText);
                }
                else
                {
                    row.Visible = string.IsNullOrEmpty(searchText);
                }
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
                    lblStatus.Text = "Status: Connected";
                    lblStatus.ForeColor = System.Drawing.Color.Green;
                    btnConnect.Enabled = false;
                    btnDisconnect!.Enabled = true;

                    // Carregar tags com informações (nome e descrição)
                    var tagInfos = await Task.Run(() => connector.GetAllTagInfos());

                    if (tagInfos != null && tagInfos.Count > 0)
                    {
                        // Limpar grid
                        tagsGridView!.Invoke((MethodInvoker)delegate
                        {
                            tagsGridView.Rows.Clear();
                        });

                        // Adicionar tags ao grid
                        foreach (var tagInfo in tagInfos)
                        {
                            tagsGridView!.Invoke((MethodInvoker)delegate
                            {
                                tagsGridView.Rows.Add(tagInfo.Name, tagInfo.Description ?? "", "---", "---", "---", "Simulate", "Set", "Reset");
                            });
                        }

                    // Iniciar atualização em tempo real
                    cancellationTokenSource = new CancellationTokenSource();
                    StartRealTimeUpdates(cancellationTokenSource.Token);
                    
                    // Iniciar timer para escrever tags simuladas a cada 5 segundos
                    StartSimulationWriteTimer();
                    }
                    else
                    {
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
            if (connector == null || tagsGridView == null) return;

            try
            {
                while (!cancellationToken.IsCancellationRequested && connector.IsConnected)
                {
                    await Task.Run(async () =>
                    {
                        // Ler valores de todas as tags
                        var tagValues = await Task.Run(() => connector.ReadAllTags());

                        if (tagValues != null && tagValues.Count > 0)
                        {
                            tagsGridView.Invoke((MethodInvoker)delegate
                            {
                                foreach (var kvp in tagValues)
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
                                            }
                                            catch
                                            {
                                                // Em caso de erro, usar valor real
                                                row.Cells["Value"].Value = kvp.Value.Value?.ToString() ?? "---";
                                                row.Cells["Quality"].Value = kvp.Value.Quality ?? "---";
                                                row.Cells["Timestamp"].Value = kvp.Value.Timestamp?.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? "---";
                                            }
                                        }
                                        else
                                        {
                                            // Usar valor real da tag
                                            row.Cells["Value"].Value = kvp.Value.Value?.ToString() ?? "---";
                                            row.Cells["Quality"].Value = kvp.Value.Quality ?? "---";
                                            row.Cells["Timestamp"].Value = kvp.Value.Timestamp?.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? "---";
                                            
                                            // Atualizar o texto do botão para "Simular"
                                            if (row.Cells["Simulate"] is DataGridViewButtonCell btnCell)
                                            {
                                                btnCell.Value = "Simulate";
                                            }
                                        }
                                    }
                                }
                            });
                        }
                    });

                    // Aguardar antes da próxima atualização (100ms = 10 atualizações por segundo)
                    await Task.Delay(100, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelamento normal, não fazer nada
            }
            catch (Exception ex)
            {
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
                            System.Diagnostics.Debug.WriteLine($"Error writing simulated tag '{tagName}': {ex.Message}");
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

        private void WriteTagMenuItem_Click(object? sender, EventArgs e)
        {
            if (tagsGridView?.SelectedRows.Count == 0 || connector == null || !connector.IsConnected)
            {
                MessageBox.Show("Please select a tag to write.", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // Obter a tag selecionada
                if (tagsGridView.SelectedRows.Count == 0)
                    return;
                    
                var selectedRow = tagsGridView.SelectedRows[0];
                string tagName = selectedRow?.Cells["TagName"]?.Value?.ToString() ?? "";
                string currentValue = selectedRow?.Cells["Value"]?.Value?.ToString() ?? "";

                if (string.IsNullOrEmpty(tagName))
                {
                    MessageBox.Show("Could not identify the selected tag.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Criar diálogo para inserir valor
                using (var inputDialog = new Form())
                {
                    inputDialog.Text = $"Write value to tag: {tagName}";
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
                        // Escrever valor na tag
                        connector.WriteTag(tagName, txtValue.Text);

                        MessageBox.Show($"Value '{txtValue.Text}' written successfully to tag '{tagName}'.", "Success",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);

                        // Atualizar o valor na grid imediatamente
                        if (selectedRow != null)
                        {
                            selectedRow.Cells["Value"].Value = txtValue.Text;
                            selectedRow.Cells["Timestamp"].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error writing tag:\n\n{ex.Message}", "Error",
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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            cancellationTokenSource?.Cancel();
            StopSimulationWriteTimer();
            connector?.Disconnect();
            tagSimulator?.ClearAll();
            base.OnFormClosing(e);
        }
    }
}
