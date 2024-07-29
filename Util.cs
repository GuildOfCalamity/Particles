using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Particles
{
    public static class Util
    {
        private static readonly WeakReference s_random = new WeakReference(null);

        [DllImport("DwmApi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, int[] attrValue, int attrSize);

        public static void UseDarkTitleBar(IntPtr hWnd)
        {
            if (hWnd != IntPtr.Zero) // zero = invalid handle
            {
                if (DwmSetWindowAttribute(hWnd, 19, new[] { 1 }, sizeof(int)) != 0)
                    DwmSetWindowAttribute(hWnd, 20, new[] { 1 }, sizeof(int));
            }
        }

        public static string NameOf(this object o)
        {
            return $"{o.GetType().Name} --> {o.GetType().BaseType.Name}";
            // Similar: System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name
        }

        /// <summary>
        /// Find & return a WinForm control based on its resource key name.
        /// </summary>
        public static T FindControl<T>(this System.Windows.Controls.Control control, string resourceKey) where T : System.Windows.Controls.Control
        {
            return (T)control.FindResource(resourceKey);
        }

        /// <summary>
        /// Find & return a WPF control based on its resource key name.
        /// </summary>
        public static T FindControl<T>(this System.Windows.FrameworkElement control, string resourceKey) where T : System.Windows.FrameworkElement
        {
            return (T)control.FindResource(resourceKey);
        }

        /// <summary>
        /// Returns an hash aggregation of an array of elements.
        /// </summary>
        /// <param name="items">An array of elements from which to create a hash.</param>
        public static int GetHashCode(params object[] items)
        {
            items = items ?? new object[0];

            return items
                .Select(item => (item == null) ? 0 : item.GetHashCode())
                .Aggregate(0, (current, next) =>
                {
                    unchecked
                    {
                        return (current * 397) ^ next;
                    }
                });
        }

        /// <summary>
        ///     Wraps <see cref="Interlocked.CompareExchange{T}(ref T,T,T)"/>
        ///     for atomically setting null fields.
        /// </summary>
        /// <typeparam name="T">The type of the field to set.</typeparam>
        /// <param name="location">
        ///     The field that, if null, will be set to <paramref name="value"/>.
        /// </param>
        /// <param name="value">
        ///     If <paramref name="location"/> is null, the object to set it to.
        /// </param>
        /// <returns>true if <paramref name="location"/> was null and has now been set; otherwise, false.</returns>
        [Obsolete("The name of this method is pretty wrong. Use InterlockedSetNullField instead.")]
        public static bool InterlockedSetIfNotNull<T>(ref T location, T value) where T : class
        {
            return InterlockedSetNullField<T>(ref location, value);
        }

        public static T GetEnumValue<T>(string enumName, bool ignoreCase = false)
        {
            ThrowUnless(typeof(T).IsEnum);
            return (T)Enum.Parse(typeof(T), enumName, ignoreCase);
        }

        /// <remarks>This will blow up wonderfully at runtime if T is not an enum type.</remarks>
        public static Dictionary<T, string> EnumToDictionary<T>()
        {
            return GetEnumValues<T>().ToDictionary(v => v, v => Enum.GetName(typeof(T), v));
        }

        public static IEnumerable<TEnum> GetEnumValues<TEnum>()
        {
            var type = typeof(TEnum);
            ThrowUnless(type.IsEnum, "The provided type must be an enum");

#if SILVERLIGHT
            return GetEnumFields(type).Select(fi => fi.GetRawConstantValue()).Cast<TEnum>();
#else
            return Enum.GetValues(type).Cast<TEnum>();
#endif
        }

        /// <remarks>If a field doesn't have the defined attribute, null is provided. If a field has an attribute more than once, it causes an exception.</remarks>
        public static IDictionary<TEnum, TAttribute> GetEnumValueAttributes<TEnum, TAttribute>() where TAttribute : Attribute
        {
            var type = typeof(TEnum);
            ThrowUnless(type.IsEnum, "The provided type must be an enum");
            return GetEnumFields(type).ToDictionary(f => (TEnum)f.GetRawConstantValue(), f => f.GetCustomAttributes<TAttribute>(false).FirstOrDefault());
        }

        /// <summary>
        ///     Wraps <see cref="Interlocked.CompareExchange{T}(ref T,T,T)"/>
        ///     for atomically setting null fields.
        /// </summary>
        /// <typeparam name="T">The type of the field to set.</typeparam>
        /// <param name="location">
        ///     The field that, if null, will be set to <paramref name="value"/>.
        /// </param>
        /// <param name="value">
        ///     If <paramref name="location"/> is null, the object to set it to.
        /// </param>
        /// <returns>true if <paramref name="location"/> was null and has now been set; otherwise, false.</returns>
        public static bool InterlockedSetNullField<T>(ref T location, T value) where T : class
        {

            // Strictly speaking, this null check is not nessesary, but
            // while CompareExchange is fast, it's still much slower than a
            // null check.
            if (location == null)
            {
                // This is a paranoid method. In a multi-threaded environment, it's possible
                // for two threads to get through the null check before a value is set.
                // This makes sure than one and only one value is set to field.
                // This is super important if the field is used in locking, for instance.

                var valueWhenSet = Interlocked.CompareExchange<T>(ref location, value, null);
                return (valueWhenSet == null);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Returns true if the provided <see cref="Exception"/> is considered 'critical'
        /// </summary>
        /// <param name="exception">The <see cref="Exception"/> to evaluate for critical-ness.</param>
        /// <returns>true if the Exception is conisdered critical; otherwise, false.</returns>
        /// <remarks>
        /// These exceptions are consider critical:
        /// <list type="bullets">
        ///     <item><see cref="OutOfMemoryException"/></item>
        ///     <item><see cref="StackOverflowException"/></item>
        ///     <item><see cref="ThreadAbortException"/></item>
        ///     <item><see cref="System.Runtime.InteropServices.SEHException"/></item>
        /// </list>
        /// </remarks>
        public static bool IsCriticalException(this Exception exception)
        {
            // Copied with respect from WPF WindowsBase->MS.Internal.CriticalExceptions.IsCriticalException
            // NullReferencException, SecurityException --> not going to consider these critical
            while (exception != null)
            {
                if (exception is OutOfMemoryException ||
                        exception is StackOverflowException ||
                        exception is ThreadAbortException
#if !WP7
 || exception is System.Runtime.InteropServices.SEHException
#endif
)
                {
                    return true;
                }
                exception = exception.InnerException;
            }
            return false;
        } //*** static IsCriticalException

        public static Random Rnd
        {
            get
            {
                var r = (Random)s_random.Target;
                if (r == null)
                {
                    s_random.Target = r = new Random();
                }
                return r;
            }
        }

        [DebuggerStepThrough]
        public static void ThrowUnless(bool truth, string message = null)
        {
            ThrowUnless<Exception>(truth, message);
        }

        [DebuggerStepThrough]
        public static void ThrowUnless<TException>(bool truth, string message) where TException : Exception
        {
            if (!truth)
            {
                throw InstanceFactory.CreateInstance<TException>(message);
            }
        }

        [DebuggerStepThrough]
        public static void ThrowUnless<TException>(bool truth) where TException : Exception, new()
        {
            if (!truth)
            {
                throw new TException();
            }
        }

        private static IEnumerable<FieldInfo> GetEnumFields(Type enumType)
        {
            ThrowUnless(enumType.IsEnum, "The provided type must be an enum");
            return enumType.GetFields(BindingFlags.Public | BindingFlags.Static);
        }



    }

    public static class InstanceFactory
    {
        /// <summary>
        /// A generic convenience method to create the provided type.
        /// </summary>
        public static T CreateInstance<T>(params object[] args)
        {
            return (T)typeof(T).CreateInstance(args);
        }

        /// <summary>
        /// A convenience extension method for Type that calls Activator.CreateInstance
        /// </summary>
        /// <returns>A new instance of the provided object.</returns>
        public static object CreateInstance(this Type type, params object[] args)
        {
            return Activator.CreateInstance(type, args);
        }
    }

}
