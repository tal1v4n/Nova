using System;
using System.Linq;
using NUnit.Framework;

namespace Nova.Tests
{
    public class ArchitectureTests
    {
        // Criterio de salida de la fase 0: un test vacío corre en EditMode.
        [Test]
        public void EditModeRunnerWorks()
        {
            Assert.Pass();
        }

        // Principio 4 de 15.1: las reglas de Core son testeables sin Unity,
        // así que Nova.Core no puede referenciar ningún ensamblado de UnityEngine.
        [Test]
        public void CoreDoesNotReferenceUnityEngine()
        {
            var core = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Nova.Core");
            Assert.IsNotNull(core, "Nova.Core no está cargado: desde T1-01 tiene scripts y debe compilar.");

            var unityReferences = core.GetReferencedAssemblies()
                .Where(r => r.Name.StartsWith("UnityEngine") || r.Name.StartsWith("UnityEditor"))
                .Select(r => r.Name)
                .ToArray();

            Assert.IsEmpty(unityReferences);
        }
    }
}
