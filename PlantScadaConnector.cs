using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace CoddCtTools
{
    /// <summary>
    /// Classe para gerenciar conexão com PlantScada via CtApi x64
    /// </summary>
    public class PlantScadaConnector
    {
        private IntPtr connectionHandle = IntPtr.Zero;
        private bool isConnected = false;
        private static readonly object logLock = new object();

        // Cache para leitura em lote (ctList)
        private IntPtr tagListHandle = IntPtr.Zero;
        private List<(string TagName, IntPtr TagHandle)> tagListCache = new List<(string, IntPtr)>();
        private static readonly object tagListLock = new object();
        private static string? logFilePath = null;
        
        // Caminho padrão das DLLs do PlantScada
        private static string defaultCtApiPath = @"C:\Program Files (x86)\Schneider Electric\Power Operation\v2024\Applications\AppServices\bin";
        
        // Flag para verificar se a DLL já foi carregada
        private static bool dllLoaded = false;
        private static IntPtr dllHandle = IntPtr.Zero;
        
        // Construtor estático - executa antes de qualquer uso da classe
        static PlantScadaConnector()
        {
            // Configurar diretório das DLLs assim que a classe é carregada
            ConfigureDllPath();
        }
        
        /// <summary>
        /// Configura o caminho das DLLs e carrega a DLL
        /// </summary>
        private static void ConfigureDllPath()
        {
            try
            {
                string fullDllPath = System.IO.Path.Combine(defaultCtApiPath, "CtApi.dll");
                if (System.IO.File.Exists(fullDllPath))
                {
                    // Configurar SetDllDirectory ANTES de qualquer P/Invoke
                    SetDllDirectory(defaultCtApiPath);
                    
                    // Adicionar ao PATH
                    var envPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Process);
                    if (!string.IsNullOrEmpty(envPath) && !envPath.Split(';').Contains(defaultCtApiPath, StringComparer.OrdinalIgnoreCase))
                    {
                        Environment.SetEnvironmentVariable("Path", $"{envPath};{defaultCtApiPath}", EnvironmentVariableTarget.Process);
                    }
                }
            }
            catch
            {
                // Ignorar erros silenciosamente nesta fase
            }
        }
        
        /// <summary>
        /// Define o caminho onde estão as DLLs do CTAPI e carrega a DLL
        /// </summary>
        public static void SetCtApiPath(string? path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                defaultCtApiPath = path;
                // Reconfigurar o diretório das DLLs
                ConfigureDllPath();
                // Tentar carregar a DLL imediatamente quando o caminho é definido
                LoadCtApiDll();
            }
        }
        
        /// <summary>
        /// Carrega a DLL CtApi.dll do caminho configurado
        /// </summary>
        private static void LoadCtApiDll()
        {
            if (dllLoaded && dllHandle != IntPtr.Zero)
            {
                return; // Já carregada
            }
            
            try
            {
                string fullDllPath = System.IO.Path.Combine(defaultCtApiPath, "CtApi.dll");
                if (System.IO.File.Exists(fullDllPath))
                {
                    dllHandle = LoadLibrary(fullDllPath);
                    if (dllHandle != IntPtr.Zero)
                    {
                        dllLoaded = true;
                    }
                }
            }
            catch
            {
                // Ignorar erros silenciosamente nesta fase
            }
        }
        
        /// <summary>
        /// Obtém o caminho atual das DLLs do CTAPI
        /// </summary>
        public static string GetCtApiPath()
        {
            return defaultCtApiPath;
        }

        // Estrutura para valores de tag
        public class TagValue
        {
            public object? Value { get; set; }
            public string? Quality { get; set; }
            public DateTime? Timestamp { get; set; }
        }

        // Estrutura para informações da tag
        public class TagInfo
        {
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
        }

        // Constantes da CtApi (baseadas no enum CtOpen do projeto de referência)
        private const uint CT_OPEN_RECONNECT = 0x00000002; // Reconnect flag
        private const uint CT_OPEN_NORMAL = 0; // Modo normal
        
        // Constantes para ctGetProperty (baseadas no projeto de referência)
        private const uint CT_PROP_STRING = 129; // String property type (usado no projeto de referência)

        // Declarações P/Invoke da CtApi baseadas na implementação de referência do citect-main
        // Usando as mesmas declarações que funcionam no projeto de referência
        [DllImport("CtApi.dll", EntryPoint = "ctOpen", SetLastError = true)]
        private static extern IntPtr ctOpen(string sComputer, string sUser, string sPassword, uint nMode);

        [DllImport("CtApi.dll", EntryPoint = "ctClose", SetLastError = true)]
        private static extern bool ctClose(IntPtr hCTAPI);

        // ctTagRead - Read from tag (retorna bool: TRUE=sucesso, FALSE=erro)
        [DllImport("CtApi.dll", EntryPoint = "ctTagRead", SetLastError = true)]
        private static extern bool ctTagRead(IntPtr hCTAPI, string sTag, StringBuilder sValue, int dwLength);

        // ctTagWrite - Write to tag (retorna bool: TRUE=sucesso, FALSE=erro)
        [DllImport("CtApi.dll", EntryPoint = "ctTagWrite", SetLastError = true)]
        private static extern bool ctTagWrite(IntPtr hCTAPI, string sTag, string sValue);

        // ctFindFirstEx - Initiate a search (usar Ex version que tem cluster parameter)
        [DllImport("CtApi.dll", EntryPoint = "ctFindFirstEx", SetLastError = true)]
        private static extern IntPtr ctFindFirstEx(IntPtr hCTAPI, string szTableName, string szFilter, string szCluster, ref IntPtr pObjHnd, int dwFlags);

        // ctFindNext - Get the next search item (retorna bool: TRUE=sucesso, FALSE=fim)
        [DllImport("CtApi.dll", EntryPoint = "ctFindNext", SetLastError = true)]
        private static extern bool ctFindNext(IntPtr hnd, ref IntPtr pObjHnd);

        // ctFindClose - Close a search (retorna bool: TRUE=sucesso, FALSE=erro)
        [DllImport("CtApi.dll", EntryPoint = "ctFindClose", SetLastError = true)]
        private static extern bool ctFindClose(IntPtr hnd);

        // ctGetProperty - Get a named property (usar ref UIntPtr como no projeto de referência)
        [DllImport("CtApi.dll", EntryPoint = "ctGetProperty", SetLastError = true)]
        private static extern bool ctGetProperty(IntPtr hnd, string szName, StringBuilder pData, uint dwBufferLength, ref UIntPtr dwResultLength, uint dwType);

        // ctList* - Leitura em lote (mais eficiente que ctTagRead individual)
        [DllImport("CtApi.dll", EntryPoint = "ctListNew", SetLastError = true)]
        private static extern IntPtr ctListNew(IntPtr hCTAPI, uint dwMode);

        [DllImport("CtApi.dll", EntryPoint = "ctListAdd", SetLastError = true)]
        private static extern IntPtr ctListAdd(IntPtr hList, string sTag);

        [DllImport("CtApi.dll", EntryPoint = "ctListRead", SetLastError = true)]
        private static extern bool ctListRead(IntPtr hList, IntPtr pctOverlapped);

        [DllImport("CtApi.dll", EntryPoint = "ctListData", SetLastError = true)]
        private static extern bool ctListData(IntPtr hTag, StringBuilder pBuffer, int dwLength, uint dwMode);

        [DllImport("CtApi.dll", EntryPoint = "ctListDelete", SetLastError = true)]
        private static extern bool ctListDelete(IntPtr hTag);

        [DllImport("CtApi.dll", EntryPoint = "ctListFree", SetLastError = true)]
        private static extern bool ctListFree(IntPtr hList);

        // ctSetManagedBinDirectory - Especifica onde carregar dependências gerenciadas do CTAPI
        [DllImport("CtApi.dll", EntryPoint = "ctSetManagedBinDirectory", SetLastError = true)]
        private static extern bool ctSetManagedBinDirectory(string sPath);

        // Funções do Windows para carregar DLLs
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string? lpPathName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpLibFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);


        /// <summary>
        /// Configura o arquivo de log (opcional)
        /// </summary>
        public static void SetLogFile(string? filePath)
        {
            logFilePath = filePath;
        }

        /// <summary>
        /// Escreve uma mensagem no log
        /// </summary>
        private static void WriteLog(string message)
        {
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
            
            // Debug output
            System.Diagnostics.Debug.WriteLine(logMessage);
            Console.WriteLine(logMessage);

            // Arquivo de log
            if (!string.IsNullOrEmpty(logFilePath))
            {
                try
                {
                    lock (logLock)
                    {
                        System.IO.File.AppendAllText(logFilePath, logMessage + Environment.NewLine);
                    }
                }
                catch
                {
                    // Ignorar erros de escrita no log
                }
            }
        }

        /// <summary>
        /// Testa diferentes configurações de conexão antes de conectar de fato
        /// </summary>
        public class ConnectionTestResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = "";
            public IntPtr Handle { get; set; }
            public string Configuration { get; set; } = "";
        }

        /// <summary>
        /// Testa conexão com diferentes configurações
        /// </summary>
        public ConnectionTestResult TestConnection(string server, string username, string password, int quickTimeoutSeconds = 3)
        {
            WriteLog("=== INICIANDO TESTE DE CONEXÃO ===");
            WriteLog($"Servidor: {server ?? "(null/local)"}");
            WriteLog($"Usuário: {username}");
            WriteLog($"Senha: {(string.IsNullOrEmpty(password) ? "(vazia)" : "***")}");

            var result = new ConnectionTestResult();

            // Configurar diretório ANTES de testar conexões
            string ctApiPath = defaultCtApiPath;
            string testDllPath = System.IO.Path.Combine(ctApiPath, "CtApi.dll");
            
            if (System.IO.File.Exists(testDllPath))
            {
                WriteLog($"Configurando diretório CTAPI para teste: {ctApiPath}");
                try
                {
                    // Carregar DLL explicitamente
                    IntPtr dllHandle = LoadLibrary(testDllPath);
                    if (dllHandle != IntPtr.Zero)
                    {
                        WriteLog($"✓ DLL carregada para teste (handle: {dllHandle})");
                    }
                    
                    SetDllDirectory(ctApiPath);
                    var envPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Process);
                    if (!string.IsNullOrEmpty(envPath) && !envPath.Split(';').Contains(ctApiPath, StringComparer.OrdinalIgnoreCase))
                    {
                        Environment.SetEnvironmentVariable("Path", $"{envPath};{ctApiPath}", EnvironmentVariableTarget.Process);
                    }
                    try { ctSetManagedBinDirectory(ctApiPath); } catch { }
                }
                catch (Exception ex)
                {
                    WriteLog($"Aviso ao configurar diretório para teste: {ex.Message}");
                }
            }

            // Configurações para testar (usar uint para mode)
            var testConfigs = new List<(string? server, string user, string pass, uint mode, string desc)>
            {
                (server, username, password, CT_OPEN_NORMAL, "Modo 0 - Normal"),
                (server, username, password, CT_OPEN_RECONNECT, "Modo 1 - Com reconexão"),
                (null, username, password, CT_OPEN_NORMAL, "Servidor null - Modo 0"),
                (null, username, password, CT_OPEN_RECONNECT, "Servidor null - Modo 1"),
                ("", username, password, CT_OPEN_NORMAL, "Servidor vazio - Modo 0"),
                (server, username, "", CT_OPEN_NORMAL, "Sem senha - Modo 0"),
            };

            // Se username está vazio, tenta algumas variações
            if (string.IsNullOrWhiteSpace(username))
            {
                testConfigs.Add((server, "Admin", password, CT_OPEN_NORMAL, "Usuário Admin"));
                testConfigs.Add((server, "admin", password, CT_OPEN_NORMAL, "Usuário admin"));
                testConfigs.Add((null, "Admin", password, CT_OPEN_NORMAL, "Admin - servidor null"));
            }

            foreach (var config in testConfigs)
            {
                WriteLog($"\nTestando: {config.desc}");
                WriteLog($"  Servidor: {config.server ?? "(null)"}");
                WriteLog($"  Usuário: {config.user}");
                WriteLog($"  Modo: {config.mode}");

                try
                {
                    IntPtr testHandle = IntPtr.Zero;
                    bool completed = false;

                    var testTask = Task.Run(() =>
                    {
                        try
                        {
                            WriteLog($"  Chamando ctOpen...");
                            testHandle = ctOpen(config.server, config.user, config.pass, config.mode);
                            WriteLog($"  ctOpen retornou: {testHandle}");
                            return testHandle;
                        }
                        catch (Exception ex)
                        {
                            WriteLog($"  EXCEÇÃO em ctOpen: {ex.GetType().Name} - {ex.Message}");
                            return IntPtr.Zero;
                        }
                    });

                    completed = testTask.Wait(TimeSpan.FromSeconds(quickTimeoutSeconds));

                    if (completed)
                    {
                        testHandle = testTask.Result;
                        if (testHandle != IntPtr.Zero)
                        {
                            WriteLog($"  ✓ SUCESSO! Handle: {testHandle}");
                            result.Success = true;
                            result.Handle = testHandle;
                            result.Configuration = config.desc;
                            result.Message = $"Conexão bem-sucedida com: {config.desc}";
                            
                            // Fechar handle de teste
                            try
                            {
                                ctClose(testHandle);
                                WriteLog($"  Handle de teste fechado");
                            }
                            catch { }

                            return result;
                        }
                        else
                        {
                            int lastError = Marshal.GetLastWin32Error();
                            WriteLog($"  ✗ Falhou - Handle = 0, Erro: {lastError}");
                        }
                    }
                    else
                    {
                        WriteLog($"  ✗ Timeout após {quickTimeoutSeconds}s");
                    }
                }
                catch (Exception ex)
                {
                    WriteLog($"  ✗ EXCEÇÃO: {ex.GetType().Name} - {ex.Message}");
                }
            }

            WriteLog("\n=== TODOS OS TESTES FALHARAM ===");
            result.Success = false;
            result.Message = "Nenhuma configuração de conexão funcionou. Verifique se o PlantScada está rodando e as credenciais estão corretas.";
            return result;
        }

        /// <summary>
        /// Conecta ao servidor PlantScada com timeout
        /// </summary>
        public bool Connect(string server, string username, string password, int timeoutSeconds = 10)
        {
            try
            {
                // Fechar conexão anterior se existir
                if (isConnected && connectionHandle != IntPtr.Zero)
                {
                    Disconnect();
                }

                // Usar o caminho configurado das DLLs do CTAPI
                string ctApiPath = defaultCtApiPath;
                string dllPath = System.IO.Path.Combine(ctApiPath, "CtApi.dll");
                
                // Verificar se a DLL existe no caminho configurado
                if (!System.IO.File.Exists(dllPath))
                {
                    // Tentar no diretório do executável como fallback
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string fallbackDllPath = System.IO.Path.Combine(baseDir, "CtApi.dll");
                    if (System.IO.File.Exists(fallbackDllPath))
                    {
                        WriteLog($"DLL não encontrada em {ctApiPath}, usando diretório do executável: {baseDir}");
                        ctApiPath = baseDir;
                        dllPath = fallbackDllPath;
                    }
                    else
                    {
                        throw new Exception($"DLL CtApi.dll não encontrada em:\n{ctApiPath}\n\nou em:\n{baseDir}\n\nVerifique o caminho configurado ou copie as DLLs para o diretório do executável.");
                    }
                }
                else
                {
                    WriteLog($"✓ DLL encontrada em: {ctApiPath}");
                }

                // Configurar diretório ANTES de chamar ctOpen
                // IMPORTANTE: Carregar a DLL explicitamente do caminho configurado
                try
                {
                    WriteLog($"Configurando diretório CTAPI: {ctApiPath}");
                    
                    // 1. Carregar a DLL explicitamente do caminho configurado
                    string fullDllPath = System.IO.Path.Combine(ctApiPath, "CtApi.dll");
                    WriteLog($"Tentando carregar DLL: {fullDllPath}");
                    
                    IntPtr dllHandle = LoadLibrary(fullDllPath);
                    if (dllHandle == IntPtr.Zero)
                    {
                        int error = Marshal.GetLastWin32Error();
                        WriteLog($"ERRO ao carregar DLL: Código {error}");
                        throw new Exception($"Não foi possível carregar CtApi.dll de:\n{fullDllPath}\n\nErro do Windows: {error}\n\nVerifique se o caminho está correto e se a DLL existe.");
                    }
                    else
                    {
                        WriteLog($"✓ DLL carregada com sucesso (handle: {dllHandle})");
                    }
                    
                    // 2. Usar SetDllDirectory do Windows para dependências
                    bool setDllDirResult = SetDllDirectory(ctApiPath);
                    WriteLog($"SetDllDirectory resultado: {setDllDirResult}");
                    
                    // 3. Adicionar o caminho das DLLs ao PATH do processo
                    var envPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Process);
                    if (!string.IsNullOrEmpty(envPath))
                    {
                        var pathArray = envPath.Split(';', StringSplitOptions.RemoveEmptyEntries);
                        if (!pathArray.Contains(ctApiPath, StringComparer.OrdinalIgnoreCase))
                        {
                            Environment.SetEnvironmentVariable("Path", $"{envPath};{ctApiPath}", EnvironmentVariableTarget.Process);
                            WriteLog($"✓ Diretório adicionado ao PATH: {ctApiPath}");
                        }
                        else
                        {
                            WriteLog($"Diretório já está no PATH: {ctApiPath}");
                        }
                    }
                    else
                    {
                        Environment.SetEnvironmentVariable("Path", ctApiPath, EnvironmentVariableTarget.Process);
                        WriteLog($"✓ PATH configurado: {ctApiPath}");
                    }
                    
                    // 4. Configurar dependências gerenciadas do CTAPI
                    try
                    {
                        bool result = ctSetManagedBinDirectory(ctApiPath);
                        WriteLog($"ctSetManagedBinDirectory: {result}");
                    }
                    catch (DllNotFoundException)
                    {
                        WriteLog($"Aviso: ctSetManagedBinDirectory não encontrada (versão antiga do CTAPI?)");
                    }
                    catch (EntryPointNotFoundException)
                    {
                        WriteLog($"Aviso: ctSetManagedBinDirectory não disponível nesta versão");
                    }
                }
                catch (Exception ex)
                {
                    WriteLog($"ERRO ao configurar diretório: {ex.Message}");
                    throw; // Relançar para que o erro seja propagado
                }

                // Conectar usando ctOpen
                // Baseado no teste, a configuração que funciona é: Servidor null - Modo 0
                // sComputer: null para servidor local (passar como string null)
                // nMode: 0 = normal (foi o que funcionou no teste) - usar uint
                string serverName = string.IsNullOrWhiteSpace(server) || server.ToLower() == "localhost" ? null : server;
                string userName = username ?? "";
                string passwordStr = password ?? "";
                uint connectionMode = CT_OPEN_NORMAL; // Modo 0 (normal) - foi o que funcionou no teste
                
                // Tentar conectar com timeout
                IntPtr handle = IntPtr.Zero;
                Exception? connectException = null;
                
                // Log de debug
                WriteLog($"=== TENTANDO CONECTAR ===");
                WriteLog($"Servidor: {serverName ?? "(null/local)"}");
                WriteLog($"Usuário: {username}");
                WriteLog($"Senha: {(string.IsNullOrEmpty(passwordStr) ? "(vazia)" : "***")}");
                WriteLog($"Timeout: {timeoutSeconds}s");
                WriteLog($"Modo: {connectionMode} (Normal - mesma configuração do teste que funcionou)");
                
                try
                {
                    // Executar ctOpen em uma task com timeout
                    var connectTask = Task.Run(() =>
                    {
                        try
                        {
                            WriteLog("Chamando ctOpen...");
                            // ctOpen: passar null como null (não usar ?? aqui, passar diretamente)
                            IntPtr result = ctOpen(serverName, userName, passwordStr, connectionMode);
                            WriteLog($"ctOpen retornou: {result}");
                            return result;
                        }
                        catch (Exception ex)
                        {
                            WriteLog($"EXCEÇÃO em ctOpen: {ex.GetType().Name} - {ex.Message}");
                            if (ex.InnerException != null)
                            {
                                WriteLog($"  InnerException: {ex.InnerException.Message}");
                            }
                            connectException = ex;
                            return IntPtr.Zero;
                        }
                    });

                    // Aguardar com timeout
                    WriteLog($"Aguardando resposta (timeout: {timeoutSeconds}s)...");
                    bool completed = connectTask.Wait(TimeSpan.FromSeconds(timeoutSeconds));
                    WriteLog($"Task completou: {completed}");
                    
                    if (completed)
                    {
                        handle = connectTask.Result;
                        WriteLog($"Handle recebido: {handle}");
                    }
                    else
                    {
                        // Task não completou no tempo - pode ser que o servidor não está respondendo
                        WriteLog("✗ TIMEOUT na conexão!");
                        throw new TimeoutException($"Tempo limite excedido ao conectar ao servidor ({timeoutSeconds}s).\n\nPossíveis causas:\n- Servidor PlantScada não está rodando\n- Servidor não está acessível (verifique o endereço)\n- Firewall bloqueando a conexão\n- Credenciais incorretas (pode causar timeout em algumas versões)\n- Serviço PlantScada não está iniciado\n\nUse o método TestConnection() para diagnosticar o problema.");
                    }
                }
                catch (TimeoutException)
                {
                    throw; // Re-throw timeout exception
                }
                catch (DllNotFoundException dllEx)
                {
                    throw new Exception($"DLL CtApi.dll não encontrada ou dependências faltando.\n\nErro: {dllEx.Message}\n\nCertifique-se de que:\n- CtApi.dll está no diretório da aplicação\n- Todas as DLLs dependentes estão presentes\n- A arquitetura (x64) está correta", dllEx);
                }
                catch (BadImageFormatException imgEx)
                {
                    throw new Exception($"Erro de formato da DLL. Certifique-se de que está usando a versão x64 da CtApi.dll.\n\nErro: {imgEx.Message}", imgEx);
                }
                catch (System.EntryPointNotFoundException entryEx)
                {
                    throw new Exception($"Função ctOpen não encontrada na DLL. A DLL pode estar corrompida, ser de uma versão incompatível, ou a CallingConvention pode estar incorreta.\n\nErro: {entryEx.Message}\n\nTente verificar se a DLL é compatível.", entryEx);
                }

                if (handle == IntPtr.Zero)
                {
                    isConnected = false;
                    int lastError = Marshal.GetLastWin32Error();
                    
                    // Se houve exceção durante a conexão, usar sua mensagem
                    if (connectException != null)
                    {
                        throw connectException;
                    }
                    
                    string errorMsg = $"Falha ao conectar (handle = 0, código de erro: {lastError}).\n\nPossíveis causas:\n- Credenciais inválidas (usuário/senha)\n- Servidor não está acessível\n- Serviço PlantScada não está rodando\n- Permissões insuficientes do usuário\n- CallingConvention incorreta na declaração P/Invoke";
                    throw new Exception(errorMsg);
                }

                connectionHandle = handle;
                isConnected = true;
                WriteLog("✓ CONEXÃO ESTABELECIDA COM SUCESSO!");
                return true;
            }
            catch (Exception)
            {
                isConnected = false;
                connectionHandle = IntPtr.Zero;
                // Re-throw com mensagem mais detalhada
                throw;
            }
        }

        /// <summary>
        /// Desconecta do servidor
        /// </summary>
        public void Disconnect()
        {
            try
            {
                FreeTagList();
                if (connectionHandle != IntPtr.Zero)
                {
                    bool result = ctClose(connectionHandle);
                    if (!result)
                    {
                        // Retorno false indica erro na CTAPI
                        int lastError = Marshal.GetLastWin32Error();
                        WriteLog($"Aviso: ctClose retornou erro (código: {lastError})");
                    }
                    
                    connectionHandle = IntPtr.Zero;
                    isConnected = false;
                }
            }
            catch (Exception ex)
            {
                // Garantir que o estado seja limpo mesmo em caso de erro
                connectionHandle = IntPtr.Zero;
                isConnected = false;
                throw new Exception($"Erro ao desconectar: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Verifica se está conectado
        /// </summary>
        public bool IsConnected
        {
            get
            {
                if (!isConnected || connectionHandle == IntPtr.Zero)
                    return false;

                // TODO: Verificar conexão real
                // return CtApi_IsConnected(connectionHandle) != 0;
                
                return isConnected;
            }
        }

        /// <summary>
        /// Obtém lista de todas as tags configuradas no projeto
        /// Usa ctFindFirst/ctFindNext para buscar na tabela Tag do Citect
        /// </summary>
        public List<string> GetAllTags()
        {
            var tagInfos = GetAllTagInfos();
            return tagInfos.Select(t => t.Name).ToList();
        }

        /// <summary>
        /// Obtém lista de todas as tags com informações (nome e descrição)
        /// </summary>
        public List<TagInfo> GetAllTagInfos()
        {
            if (!IsConnected || connectionHandle == IntPtr.Zero)
                throw new Exception("Não conectado ao servidor.");

            var tagList = new List<TagInfo>();
            IntPtr searchHandle = IntPtr.Zero;

            try
            {
                // Usar ctFindFirst para iniciar busca na tabela "Tag"
                // szFilter vazio "" retorna todas as tags
                IntPtr firstObjHnd = IntPtr.Zero;
                
                // Tentar diferentes nomes de tabela possíveis
                string[] tableNames = { "Tag", "Tags", "TagTable", "IO_TAG", "IOTAG" };
                bool found = false;

                foreach (string tableName in tableNames)
                {
                    // Usar ctFindFirstEx que tem o parâmetro cluster (passar null ou "" para cluster padrão)
                    IntPtr objHnd = IntPtr.Zero;
                    searchHandle = ctFindFirstEx(connectionHandle, tableName, "", "", ref objHnd, 0);
                    firstObjHnd = objHnd;
                    
                    // ctFindFirstEx retorna handle da busca (não-zero = sucesso)
                    // firstObjHnd será o handle do primeiro objeto encontrado
                    if (searchHandle != IntPtr.Zero)
                    {
                        found = true;
                        break; // Encontrou uma tabela válida
                    }
                }

                if (!found || searchHandle == IntPtr.Zero)
                {
                    throw new Exception("Não foi possível iniciar busca de tags. Verifique se há tags configuradas no projeto e se o nome da tabela está correto.");
                }

                // Processar primeiro item se existir
                if (firstObjHnd != IntPtr.Zero)
                {
                    ProcessTagObject(firstObjHnd, tagList);
                }

                // Buscar próximos itens
                IntPtr currentObjHnd = IntPtr.Zero;
                bool continueSearch = true;

                while (continueSearch)
                {
                    IntPtr nextObjHnd = IntPtr.Zero;
                    bool findNextResult = ctFindNext(searchHandle, ref nextObjHnd);
                    
                    // ctFindNext retorna true em caso de sucesso, false quando não há mais itens
                    if (!findNextResult || nextObjHnd == IntPtr.Zero)
                    {
                        continueSearch = false; // Fim da busca
                    }
                    else
                    {
                        ProcessTagObject(nextObjHnd, tagList);
                    }
                }

                return tagList;
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao obter lista de tags: {ex.Message}", ex);
            }
            finally
            {
                // Sempre fechar a busca
                if (searchHandle != IntPtr.Zero)
                {
                    try
                    {
                        ctFindClose(searchHandle);
                    }
                    catch
                    {
                        // Ignorar erros ao fechar
                    }
                }
            }
        }

        /// <summary>
        /// Lê valores de todas as tags usando ReadTag
        /// </summary>
        public Dictionary<string, TagValue> ReadAllTags()
        {
            if (!IsConnected || connectionHandle == IntPtr.Zero)
                throw new Exception("Não conectado ao servidor.");

            try
            {
                var tags = GetAllTags();
                var result = new Dictionary<string, TagValue>();

                foreach (var tagName in tags)
                {
                    try
                    {
                        // Usar o método ReadTag que usa ctTagRead corretamente
                        TagValue tagValue = ReadTag(tagName);
                        result[tagName] = tagValue;
                    }
                    catch (Exception ex)
                    {
                        // Erro ao ler tag específica, adicionar com erro
                        result[tagName] = new TagValue
                        {
                            Value = null,
                            Quality = $"Error: {ex.Message}",
                            Timestamp = DateTime.Now
                        };
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao ler tags: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Lê valor de uma tag específica usando ctTagRead
        /// </summary>
        public TagValue ReadTag(string tagName)
        {
            if (!IsConnected || connectionHandle == IntPtr.Zero)
                throw new Exception("Não conectado ao servidor.");

            try
            {
                // ctTagRead retorna o valor como string
                StringBuilder valueBuffer = new StringBuilder(256);
                bool result = ctTagRead(connectionHandle, tagName, valueBuffer, valueBuffer.Capacity);

                // Na CTAPI, retorno true indica sucesso, false indica erro
                if (result)
                {
                    string valueStr = valueBuffer.ToString().Trim('\0');
                    
                    // Tentar converter para double se for numérico
                    object tagValue = valueStr;
                    if (double.TryParse(valueStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dValue))
                    {
                        tagValue = dValue;
                    }

                    return new TagValue
                    {
                        Value = tagValue,
                        Quality = "Good", // ctTagRead não retorna quality diretamente nesta versão
                        Timestamp = DateTime.Now
                    };
                }
                else
                {
                    // Retorno false indica erro na CTAPI
                    int lastError = Marshal.GetLastWin32Error();
                    throw new Exception($"Falha ao ler tag '{tagName}'. Código de erro: {lastError}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao ler tag '{tagName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Lê múltiplas tags em uma única chamada ao servidor (muito mais eficiente que ReadTag em loop).
        /// Usa ctListNew/ctListAdd/ctListRead/ctListData para leitura em lote.
        /// </summary>
        public Dictionary<string, TagValue> ReadTagsBatch(IList<string> tagNames)
        {
            if (!IsConnected || connectionHandle == IntPtr.Zero)
                throw new Exception("Não conectado ao servidor.");

            if (tagNames == null || tagNames.Count == 0)
                return new Dictionary<string, TagValue>();

            var result = new Dictionary<string, TagValue>(tagNames.Count);
            var timestamp = DateTime.Now;

            lock (tagListLock)
            {
                try
                {
                    if (!EnsureTagList(tagNames))
                    {
                        // Fallback para leitura individual se ctList falhar
                        foreach (var tagName in tagNames)
                        {
                            try
                            {
                                result[tagName] = ReadTag(tagName);
                            }
                            catch (Exception ex)
                            {
                                result[tagName] = new TagValue
                                {
                                    Value = null,
                                    Quality = $"Error: {ex.Message}",
                                    Timestamp = timestamp
                                };
                            }
                        }
                        return result;
                    }

                    // ctListRead - uma única chamada para ler todas as tags
                    if (!ctListRead(tagListHandle, IntPtr.Zero))
                    {
                        int lastError = Marshal.GetLastWin32Error();
                        WriteLog($"ctListRead falhou (erro {lastError}), usando fallback individual");
                        FreeTagList();
                        foreach (var tagName in tagNames)
                        {
                            try { result[tagName] = ReadTag(tagName); }
                            catch (Exception ex)
                            {
                                result[tagName] = new TagValue { Value = null, Quality = $"Error: {ex.Message}", Timestamp = timestamp };
                            }
                        }
                        return result;
                    }

                    // ctListData - obter valor de cada tag (dados já em memória local após ctListRead)
                    var buffer = new StringBuilder(256);
                    foreach (var (tagName, tagHandle) in tagListCache)
                    {
                        try
                        {
                            buffer.Clear();
                            if (ctListData(tagHandle, buffer, buffer.Capacity, 0))
                            {
                                string valueStr = buffer.ToString().Trim('\0');
                                object tagValue = valueStr;
                                if (double.TryParse(valueStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dValue))
                                    tagValue = dValue;

                                result[tagName] = new TagValue
                                {
                                    Value = tagValue,
                                    Quality = "Good",
                                    Timestamp = timestamp
                                };
                            }
                            else
                            {
                                int err = Marshal.GetLastWin32Error();
                                result[tagName] = new TagValue { Value = null, Quality = $"Error: {err}", Timestamp = timestamp };
                            }
                        }
                        catch (Exception ex)
                        {
                            result[tagName] = new TagValue { Value = null, Quality = $"Error: {ex.Message}", Timestamp = timestamp };
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteLog($"ReadTagsBatch erro: {ex.Message}");
                    foreach (var tagName in tagNames)
                    {
                        if (!result.ContainsKey(tagName))
                            result[tagName] = new TagValue { Value = null, Quality = $"Error: {ex.Message}", Timestamp = timestamp };
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Cria ou atualiza a lista de tags para leitura em lote. Retorna true se bem-sucedido.
        /// </summary>
        private bool EnsureTagList(IList<string> tagNames)
        {
            var tagSet = new HashSet<string>(tagNames, StringComparer.OrdinalIgnoreCase);
            var cacheSet = new HashSet<string>(tagListCache.Select(x => x.TagName), StringComparer.OrdinalIgnoreCase);

            if (tagListHandle != IntPtr.Zero && tagSet.SetEquals(cacheSet))
                return true;

            FreeTagList();

            tagListHandle = ctListNew(connectionHandle, 0);
            if (tagListHandle == IntPtr.Zero)
            {
                WriteLog("ctListNew retornou null, leitura em lote indisponível");
                return false;
            }

            foreach (var tagName in tagNames)
            {
                if (string.IsNullOrEmpty(tagName)) continue;
                var hTag = ctListAdd(tagListHandle, tagName);
                if (hTag != IntPtr.Zero)
                    tagListCache.Add((tagName, hTag));
            }

            if (tagListCache.Count == 0)
            {
                ctListFree(tagListHandle);
                tagListHandle = IntPtr.Zero;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Libera a lista de tags usada para leitura em lote.
        /// </summary>
        private void FreeTagList()
        {
            if (tagListHandle != IntPtr.Zero)
            {
                try
                {
                    foreach (var (_, hTag) in tagListCache)
                    {
                        try { ctListDelete(hTag); } catch { }
                    }
                    tagListCache.Clear();
                    ctListFree(tagListHandle);
                }
                catch { }
                tagListHandle = IntPtr.Zero;
            }
            tagListCache.Clear();
        }

        /// <summary>
        /// Escreve valor em uma tag específica usando ctTagWrite
        /// </summary>
        public void WriteTag(string tagName, string value)
        {
            if (!IsConnected || connectionHandle == IntPtr.Zero)
                throw new Exception("Não conectado ao servidor.");

            try
            {
                WriteLog($"Escrevendo tag '{tagName}' com valor '{value}'");
                
                // ctTagWrite retorna bool: TRUE=sucesso, FALSE=erro
                bool result = ctTagWrite(connectionHandle, tagName, value);

                if (result)
                {
                    WriteLog($"✓ Tag '{tagName}' escrita com sucesso: {value}");
                }
                else
                {
                    // Retorno false indica erro na CTAPI
                    int lastError = Marshal.GetLastWin32Error();
                    throw new Exception($"Falha ao escrever tag '{tagName}'. Código de erro: {lastError}");
                }
            }
            catch (Exception ex)
            {
                WriteLog($"✗ Erro ao escrever tag '{tagName}': {ex.Message}");
                throw new Exception($"Erro ao escrever tag '{tagName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Processa um objeto de tag para extrair o nome (versão antiga para compatibilidade)
        /// </summary>
        private void ProcessTagObject(IntPtr objHnd, List<string> tagList)
        {
            var tagInfoList = new List<TagInfo>();
            ProcessTagObject(objHnd, tagInfoList);
            foreach (var tagInfo in tagInfoList)
            {
                if (!tagList.Contains(tagInfo.Name))
                {
                    tagList.Add(tagInfo.Name);
                }
            }
        }

        /// <summary>
        /// Processa um objeto de tag para extrair o nome e descrição
        /// </summary>
        private void ProcessTagObject(IntPtr objHnd, List<TagInfo> tagList)
        {
            try
            {
                string tagName = "";
                string tagDescription = "";

                // Tentar diferentes propriedades para obter o nome da tag
                string[] namePropertyNames = { "Name", "TagName", "TAG", "Tag" };
                
                foreach (string propName in namePropertyNames)
                {
                    StringBuilder tagNameBuffer = new StringBuilder(256);
                    UIntPtr resultLength = UIntPtr.Zero;
                    
                    bool result = ctGetProperty(objHnd, propName, tagNameBuffer, (uint)tagNameBuffer.Capacity, ref resultLength, CT_PROP_STRING);
                    
                    // ctGetProperty retorna true em caso de sucesso
                    if (result && resultLength.ToUInt32() > 0)
                    {
                        tagName = tagNameBuffer.ToString().Trim('\0', ' ');
                        if (!string.IsNullOrEmpty(tagName))
                        {
                            break; // Encontrou o nome
                        }
                    }
                }

                // Tentar diferentes propriedades para obter a descrição da tag
                string[] descPropertyNames = { "Description", "Desc", "Comment", "CommentText", "Comment_Text", "DESC", "COMMENT" };
                
                foreach (string propName in descPropertyNames)
                {
                    StringBuilder descBuffer = new StringBuilder(512);
                    UIntPtr resultLength = UIntPtr.Zero;
                    
                    bool result = ctGetProperty(objHnd, propName, descBuffer, (uint)descBuffer.Capacity, ref resultLength, CT_PROP_STRING);
                    
                    // ctGetProperty retorna true em caso de sucesso
                    if (result && resultLength.ToUInt32() > 0)
                    {
                        tagDescription = descBuffer.ToString().Trim('\0', ' ');
                        if (!string.IsNullOrEmpty(tagDescription))
                        {
                            break; // Encontrou a descrição
                        }
                    }
                }

                // Adicionar à lista se encontrou pelo menos o nome
                if (!string.IsNullOrEmpty(tagName))
                {
                    // Verificar se já existe na lista
                    if (!tagList.Any(t => t.Name == tagName))
                    {
                        tagList.Add(new TagInfo
                        {
                            Name = tagName,
                            Description = tagDescription
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                // Log erro mas continua processando outras tags
                WriteLog($"Erro ao processar objeto de tag: {ex.Message}");
            }
        }

        /// <summary>
        /// Converte código de qualidade para string
        /// Baseado nos códigos de qualidade padrão OPC/SCADA
        /// </summary>
        private string GetQualityString(int qualityCode)
        {
            // Códigos de qualidade comuns em sistemas SCADA/OPC
            // 0 geralmente significa Good/OK
            // Outros valores podem indicar Bad, Uncertain, etc.
            switch (qualityCode)
            {
                case 0:
                case 192: // Good (OPC)
                    return "Good";
                case 1:
                case 64: // Bad (OPC)
                    return "Bad";
                case 2:
                case 128: // Uncertain (OPC)
                    return "Uncertain";
                case 4:
                    return "Not Connected";
                case 8:
                    return "Device Failure";
                case 16:
                    return "Sensor Failure";
                case 32:
                    return "Last Known Value";
                default:
                    return qualityCode == 0 ? "Good" : $"Unknown ({qualityCode})";
            }
        }
    }
}

