// Se importan las dependencias necesarias
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq; // Asegúrate de tener este using para .Any() y .Contains() con StringComparer
using System.Threading.Tasks;

namespace FrontBlazor.Services
{
    // Definir la clase base para validar el acceso a las páginas
    public class ValidacionAcceso : ComponentBase
    {
        // Inyectar servicio de interoperabilidad con JavaScript
        [Inject] protected IJSRuntime InteropJS { get; set; } = default!;

        // Inyectar servicio para manejar la navegación
        [Inject] protected NavigationManager Navegacion { get; set; } = default!;

        // Controlar si el acceso está permitido (tu variable original)
        protected bool accesoPermitido = false; // Cambiado a protected para que las páginas hijas puedan leerlo

        // Variable para almacenar los roles del usuario actual
        private List<string> _currentUserRoles = new List<string>();
        private bool _rolesCargados = false; // Para asegurar que los roles se carguen solo una vez por instancia

        // Sobrescribir el método que se ejecuta después del primer renderizado
        protected override async Task OnAfterRenderAsync(bool primerRenderizado)
        {
            // Mejorable: en el futuro considerar validar acceso antes de renderizar para mejorar la UX
            if (primerRenderizado)
            {
                await ValidarAccesoAsync(); // Tu lógica original de validación de página
            }
            await base.OnAfterRenderAsync(primerRenderizado);
        }

        // Controlar si el componente debe ser renderizado
        protected override bool ShouldRender()
        {
            // Obtener la ruta actual eliminando la base URL y los parámetros
            var ruta = Navegacion.Uri.Replace(Navegacion.BaseUri, "/").Split('?')[0];

            // Permitir renderizado solo si está en login o si el acceso fue validado
            // Tu lógica original
            return ruta.Equals("/login", StringComparison.OrdinalIgnoreCase) || accesoPermitido;
        }

        // Validar el acceso del usuario a la página (TU LÓGICA ORIGINAL CASI INTACTA)
        private async Task ValidarAccesoAsync()
        {
            try
            {
                var ruta = Navegacion.Uri.Replace(Navegacion.BaseUri, "/").Split('?')[0];

                // Permitir acceso inmediato si se está en la página de login
                if (ruta.Equals("/login", StringComparison.OrdinalIgnoreCase))
                {
                    accesoPermitido = true;
                    _rolesCargados = false; // Resetear para la próxima navegación post-login
                    _currentUserRoles.Clear();
                    StateHasChanged(); // Para asegurar que la página de login se muestre si es necesario
                    return;
                }

                // Obtener el email del usuario desde sessionStorage
                var correoUsuario = await InteropJS.InvokeAsync<string>("sessionStorage.getItem", "usuarioEmail");

                // Redirigir a login si no hay correo en sesión
                if (string.IsNullOrEmpty(correoUsuario))
                {
                    accesoPermitido = false; // Asegurar que no se renderice contenido protegido
                    // await InteropJS.InvokeVoidAsync("alert", "Sesión no válida. Redirigiendo al login..."); // Alerta original
                    Navegacion.NavigateTo("/login", true);
                    return;
                }

                // Cargar roles del usuario si aún no se han cargado en esta instancia
                if (!_rolesCargados) 
                {
                    _currentUserRoles = await InteropJS.InvokeAsync<List<string>>("eval", @"
                        Object.keys(sessionStorage)
                              .filter(k => k.startsWith('rol_'))
                              .map(k => sessionStorage.getItem(k))") ?? new List<string>();
                    _rolesCargados = true;
                }

                // TU LÓGICA ORIGINAL PARA OBTENER RUTAS PERMITIDAS:
                var rutasPermitidas = await InteropJS.InvokeAsync<List<string>>("eval", @"
                    Object.keys(sessionStorage)
                          .filter(k => k.startsWith('ruta_'))
                          .map(k => sessionStorage.getItem(k))") ?? new List<string>();

                // MODIFICACIÓN: Si el usuario es admin, tiene acceso a todo.
                if (_currentUserRoles.Contains("admin", StringComparer.OrdinalIgnoreCase))
                {
                    accesoPermitido = true;
                }
                // TU LÓGICA ORIGINAL PARA VERIFICAR LA RUTA:
                else if (rutasPermitidas.Contains(ruta))
                {
                    accesoPermitido = true;
                }
                else
                {
                     accesoPermitido = false; // No es admin y la ruta no está explícitamente permitida
                }

                if (accesoPermitido)
                {
                    StateHasChanged(); // Permitir que la página protegida se renderice
                }
                else
                {
                    await InteropJS.InvokeVoidAsync("alert", "No tiene permisos para acceder a esta página.");
                    Navegacion.NavigateTo("/", true); // Redirigir a la página principal por defecto
                }
            }
            catch(Exception ex) // Captura más genérica para cualquier error en el proceso
            {
                Console.WriteLine($"Error en ValidacionAcceso.ValidarAccesoAsync: {ex.Message}");
                accesoPermitido = false; // Asegurar que no se renderice contenido protegido
                // Manejar cualquier error durante la validación
                // Mejorable: capturar el error con más detalle para registros o telemetría
                await InteropJS.InvokeVoidAsync("alert", "Error en la validación de acceso.");
                Navegacion.NavigateTo("/", true); // O a /login si es más apropiado
            }
        }

        /// <summary>
        /// NUEVO MÉTODO: Determina si el usuario actual tiene permisos de modificación.
        /// Los roles 'Verificador' e 'invitado' (si lo tuvieras) NO pueden modificar.
        /// El rol 'admin' SIEMPRE puede modificar.
        /// </summary>
        protected async Task<bool> UsuarioPuedeModificarAsync()
        {
            // Cargar roles si aún no se han cargado. Esto es un fallback, idealmente se cargan en ValidarAccesoAsync.
            if (!_rolesCargados)
            {
                var correo = await InteropJS.InvokeAsync<string>("sessionStorage.getItem", "usuarioEmail");
                if (string.IsNullOrEmpty(correo)) return false; // Si no hay sesión, no puede modificar

                _currentUserRoles = await InteropJS.InvokeAsync<List<string>>("eval", @"
                    Object.keys(sessionStorage)
                          .filter(k => k.startsWith('rol_'))
                          .map(k => sessionStorage.getItem(k))") ?? new List<string>();
                _rolesCargados = true;
            }
            
            if (!_currentUserRoles.Any()) return false; // Si no hay roles, no puede modificar

            // Admin siempre puede modificar
            if (_currentUserRoles.Contains("admin", StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            // Roles que NO tienen permisos de modificación
            if (_currentUserRoles.Contains("Verificador", StringComparer.OrdinalIgnoreCase) ||
                _currentUserRoles.Contains("invitado", StringComparer.OrdinalIgnoreCase)) // Si manejas un rol "invitado"
            {
                return false; // A menos que también sea admin (ya cubierto arriba)
            }
            
            // Todos los demás roles (Validador, Administrativo) SÍ pueden modificar en este modelo.
            return true;
        }
    }
}