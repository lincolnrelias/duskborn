using System.Collections;
using System.Diagnostics;

namespace Duskborn.Gameplay.World
{
    /// <summary>
    /// Gerenciador de orçamento de tempo por quadro (Frame Budgeting).
    /// Permite fatiar operações computacionais pesadas (como relevo fractal, raycasts e montagem de malhas)
    /// ao longo de vários frames para eliminar engasgos (stuttering) e manter uma taxa de 60+ FPS constante.
    /// </summary>
    public class GenerationBudget
    {
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly float _maxMillisecondsPerFrame;

        /// <summary>
        /// Inicializa o controlador de orçamento com um limite em milissegundos por quadro (padrão: 8ms, metade de um frame a 60 FPS).
        /// </summary>
        /// <param name="maxMillisecondsPerFrame">Tempo máximo em ms antes de ceder o frame ao Unity.</param>
        public GenerationBudget(float maxMillisecondsPerFrame = 8f)
        {
            _maxMillisecondsPerFrame = maxMillisecondsPerFrame;
            _stopwatch.Start();
        }

        /// <summary>
        /// Reinicia o cronômetro para o frame atual.
        /// </summary>
        public void Reset()
        {
            _stopwatch.Restart();
        }

        /// <summary>
        /// Verifica se o limite de tempo do frame atual foi atingido.
        /// Caso tenha sido atingido, reinicia o cronômetro e retorna true, sinalizando que a rotina deve dar 'yield return null'.
        /// </summary>
        public bool ShouldYield()
        {
            if (_stopwatch.ElapsedMilliseconds >= _maxMillisecondsPerFrame)
            {
                _stopwatch.Restart();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Helper que cede a execução ao Unity por um frame e reseta o cronômetro.
        /// </summary>
        public IEnumerator YieldFrame()
        {
            yield return null;
            Reset();
        }
    }
}
