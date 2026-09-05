using UnityEditor;
using UnityEditor.Compilation;

namespace MultiAgentBridge
{
    [InitializeOnLoad]
    public class CompilationCheck
    {
        private static int _errorCount = 0;
        private static bool _compiling = false;

        static CompilationCheck()
        {
            CompilationPipeline.compilationStarted += _ => {
                _compiling = true;
                _errorCount = 0;
            };
            CompilationPipeline.assemblyCompilationFinished += (path, messages) =>
            {
                foreach (var message in messages)
                {
                    if (message.type == CompilerMessageType.Error)
                    {
                        _errorCount++;
                    }
                }
            };
            CompilationPipeline.compilationFinished += _ => { _compiling = false; };
        }

        public static bool IsCompiling => _compiling;
        public static int ErrorCount => _errorCount;
    }
}