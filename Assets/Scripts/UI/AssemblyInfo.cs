using System.Runtime.CompilerServices;

// Los tests de EditMode no ejecutan OnEnable: llaman directamente a la suscripción de las vistas.
[assembly: InternalsVisibleTo("Nova.Tests")]
