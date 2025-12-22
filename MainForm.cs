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

        public MainForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Codd Automação - PlantScada / Power Operation - Tester";
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
                Text = "Procurar...",
                Location = new System.Drawing.Point(510, 11),
                Size = new System.Drawing.Size(80, 25)
            };
            btnBrowsePath.Click += (s, e) =>
            {
                using (var folderDialog = new FolderBrowserDialog())
                {
                    folderDialog.Description = "Selecione a pasta onde estão as DLLs do CTAPI (geralmente a pasta Bin do PlantScada)";
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
                Text = "Servidor:",
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
                Text = "Usuário:",
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
                Text = "Senha:",
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
                Text = "Conectar",
                Location = new System.Drawing.Point(420, 42),
                Size = new System.Drawing.Size(100, 30),
                UseVisualStyleBackColor = true
            };
            btnConnect.Click += BtnConnect_Click;

            btnDisconnect = new Button
            {
                Text = "Desconectar",
                Location = new System.Drawing.Point(420, 78),
                Size = new System.Drawing.Size(100, 30),
                Enabled = false,
                UseVisualStyleBackColor = true
            };
            btnDisconnect.Click += BtnDisconnect_Click;

            btnTestConnection = new Button
            {
                Text = "Testar Conexão",
                Location = new System.Drawing.Point(530, 42),
                Size = new System.Drawing.Size(120, 30),
                UseVisualStyleBackColor = true
            };
            btnTestConnection.Click += BtnTestConnection_Click;

            lblStatus = new Label
            {
                Text = "Status: Desconectado",
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
                Text = "Tags (Tempo Real):",
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
                Text = "Buscar:",
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

            tagsGridView.Columns.Add("TagName", "Nome da Tag");
            tagsGridView.Columns.Add("Value", "Valor");
            tagsGridView.Columns.Add("Quality", "Qualidade");
            tagsGridView.Columns.Add("Timestamp", "Data/Hora");
            tagsGridView.Columns["TagName"].FillWeight = 40;
            tagsGridView.Columns["Value"].FillWeight = 20;
            tagsGridView.Columns["Quality"].FillWeight = 20;
            tagsGridView.Columns["Timestamp"].FillWeight = 20;

            // Adicionar menu de contexto (botão direito)
            var contextMenu = new ContextMenuStrip();
            var writeMenuItem = new ToolStripMenuItem("Escrever Valor...");
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
                MessageBox.Show("Por favor, preencha todos os campos de conexão.", "Aviso", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                btnConnect!.Enabled = false;
                lblStatus!.Text = "Status: Conectando...";
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
                    lblStatus.Text = "Status: Conectado";
                    lblStatus.ForeColor = System.Drawing.Color.Green;
                    btnConnect.Enabled = false;
                    btnDisconnect!.Enabled = true;

                    // Carregar tags
                    var tags = await Task.Run(() => connector.GetAllTags());

                    if (tags != null && tags.Count > 0)
                    {
                        // Limpar grid
                        tagsGridView!.Invoke((MethodInvoker)delegate
                        {
                            tagsGridView.Rows.Clear();
                        });

                        // Adicionar tags ao grid
                        foreach (var tagName in tags)
                        {
                            tagsGridView!.Invoke((MethodInvoker)delegate
                            {
                                tagsGridView.Rows.Add(tagName, "---", "---", "---");
                            });
                        }

                        // Iniciar atualização em tempo real
                        cancellationTokenSource = new CancellationTokenSource();
                        StartRealTimeUpdates(cancellationTokenSource.Token);
                    }
                    else
                    {
                        MessageBox.Show("Nenhuma tag encontrada no projeto.", "Informação", 
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                else
                {
                    lblStatus.Text = "Status: Falha na conexão";
                    lblStatus.ForeColor = System.Drawing.Color.Red;
                    btnConnect.Enabled = true;
                    MessageBox.Show($"Falha ao conectar. Verifique o log em:\n{logPath}", "Erro", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                lblStatus!.Text = "Status: Erro";
                lblStatus.ForeColor = System.Drawing.Color.Red;
                btnConnect!.Enabled = true;
                MessageBox.Show($"Erro ao conectar:\n\n{ex.Message}", "Erro", 
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
                                        row.Cells["Value"].Value = kvp.Value.Value?.ToString() ?? "---";
                                        row.Cells["Quality"].Value = kvp.Value.Quality ?? "---";
                                        row.Cells["Timestamp"].Value = kvp.Value.Timestamp?.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? "---";
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
                        MessageBox.Show($"Erro durante atualização de tags:\n\n{ex.Message}", "Erro",
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
                connector?.Disconnect();

                btnConnect!.Enabled = true;
                btnDisconnect!.Enabled = false;
                lblStatus!.Text = "Status: Desconectado";
                lblStatus.ForeColor = System.Drawing.Color.Red;

                tagsGridView!.Rows.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao desconectar: {ex.Message}", "Erro", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
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

                lblStatus!.Text = "Status: Testando conexão...";
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
                        $"✓ Teste de conexão bem-sucedido!\n\nConfiguração que funcionou:\n{testResult.Configuration}\n\nHandle: {testResult.Handle}\n\nLog salvo em:\n{logPath}",
                        "Teste de Conexão",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    lblStatus.Text = "Status: Teste falhou";
                    lblStatus.ForeColor = System.Drawing.Color.Red;
                    MessageBox.Show(
                        $"✗ Teste de conexão falhou.\n\n{testResult.Message}\n\nLog salvo em:\n{logPath}\n\nVerifique o arquivo de log para mais detalhes.",
                        "Teste de Conexão",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                lblStatus!.Text = "Status: Erro no teste";
                lblStatus.ForeColor = System.Drawing.Color.Red;
                MessageBox.Show(
                    $"Erro ao testar conexão:\n\n{ex.Message}",
                    "Erro",
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
                MessageBox.Show("Por favor, selecione uma tag para escrever.", "Aviso",
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
                    MessageBox.Show("Não foi possível identificar a tag selecionada.", "Erro",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Criar diálogo para inserir valor
                using (var inputDialog = new Form())
                {
                    inputDialog.Text = $"Escrever valor na tag: {tagName}";
                    inputDialog.Size = new System.Drawing.Size(400, 150);
                    inputDialog.StartPosition = FormStartPosition.CenterParent;
                    inputDialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                    inputDialog.MaximizeBox = false;
                    inputDialog.MinimizeBox = false;

                    var lblValue = new Label
                    {
                        Text = "Novo valor:",
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
                        Text = "Cancelar",
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

                        MessageBox.Show($"Valor '{txtValue.Text}' escrito com sucesso na tag '{tagName}'.", "Sucesso",
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
                MessageBox.Show($"Erro ao escrever tag:\n\n{ex.Message}", "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            cancellationTokenSource?.Cancel();
            connector?.Disconnect();
            base.OnFormClosing(e);
        }
    }
}
