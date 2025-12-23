using System;
using System.Collections.Generic;
using System.Threading;

namespace CoddCtTools
{
    /// <summary>
    /// Classe para gerenciar simulação de valores de tags
    /// </summary>
    public class TagSimulator
    {
        private Dictionary<string, SimulatedTagInfo> simulatedTags;
        private Random random;
        private object lockObject;

        public TagSimulator()
        {
            simulatedTags = new Dictionary<string, SimulatedTagInfo>();
            random = new Random();
            lockObject = new object();
        }

        /// <summary>
        /// Informações de uma tag simulada
        /// </summary>
        public class SimulatedTagInfo
        {
            public string TagName { get; set; } = "";
            public double MinValue { get; set; }
            public double MaxValue { get; set; }
            public bool IsActive { get; set; } = true;
            public DateTime LastWriteTime { get; set; } = DateTime.MinValue;
        }

        /// <summary>
        /// Adiciona ou atualiza uma tag para simulação
        /// </summary>
        public void SetSimulation(string tagName, double minValue, double maxValue)
        {
            lock (lockObject)
            {
                DateTime lastWrite = DateTime.MinValue;
                if (simulatedTags.ContainsKey(tagName))
                {
                    lastWrite = simulatedTags[tagName].LastWriteTime;
                }
                
                simulatedTags[tagName] = new SimulatedTagInfo
                {
                    TagName = tagName,
                    MinValue = minValue,
                    MaxValue = maxValue,
                    IsActive = true,
                    LastWriteTime = lastWrite
                };
            }
        }
        
        /// <summary>
        /// Atualiza o horário da última escrita para uma tag
        /// </summary>
        public void UpdateLastWriteTime(string tagName)
        {
            lock (lockObject)
            {
                if (simulatedTags.ContainsKey(tagName))
                {
                    simulatedTags[tagName].LastWriteTime = DateTime.Now;
                }
            }
        }
        
        /// <summary>
        /// Verifica se deve escrever na tag (passou 5 segundos desde a última escrita)
        /// </summary>
        public bool ShouldWrite(string tagName, int intervalSeconds = 5)
        {
            lock (lockObject)
            {
                if (!simulatedTags.ContainsKey(tagName) || !simulatedTags[tagName].IsActive)
                {
                    return false;
                }
                
                var info = simulatedTags[tagName];
                TimeSpan elapsed = DateTime.Now - info.LastWriteTime;
                return elapsed.TotalSeconds >= intervalSeconds;
            }
        }

        /// <summary>
        /// Remove uma tag da simulação
        /// </summary>
        public void RemoveSimulation(string tagName)
        {
            lock (lockObject)
            {
                simulatedTags.Remove(tagName);
            }
        }

        /// <summary>
        /// Verifica se uma tag está sendo simulada
        /// </summary>
        public bool IsSimulated(string tagName)
        {
            lock (lockObject)
            {
                return simulatedTags.ContainsKey(tagName) && simulatedTags[tagName].IsActive;
            }
        }

        /// <summary>
        /// Obtém informações de simulação de uma tag
        /// </summary>
        public SimulatedTagInfo? GetSimulationInfo(string tagName)
        {
            lock (lockObject)
            {
                return simulatedTags.ContainsKey(tagName) ? simulatedTags[tagName] : null;
            }
        }

        /// <summary>
        /// Gera um valor simulado aleatório para uma tag
        /// </summary>
        public double GenerateSimulatedValue(string tagName)
        {
            lock (lockObject)
            {
                if (!simulatedTags.ContainsKey(tagName) || !simulatedTags[tagName].IsActive)
                {
                    throw new InvalidOperationException($"Tag '{tagName}' não está sendo simulada.");
                }

                var info = simulatedTags[tagName];
                double range = info.MaxValue - info.MinValue;
                
                // Gerar valor aleatório entre min e max
                double value = info.MinValue + (random.NextDouble() * range);
                
                // Arredondar para 2 casas decimais
                return Math.Round(value, 2);
            }
        }

        /// <summary>
        /// Obtém todas as tags simuladas
        /// </summary>
        public Dictionary<string, SimulatedTagInfo> GetAllSimulatedTags()
        {
            lock (lockObject)
            {
                return new Dictionary<string, SimulatedTagInfo>(simulatedTags);
            }
        }

        /// <summary>
        /// Limpa todas as simulações
        /// </summary>
        public void ClearAll()
        {
            lock (lockObject)
            {
                simulatedTags.Clear();
            }
        }
    }
}

