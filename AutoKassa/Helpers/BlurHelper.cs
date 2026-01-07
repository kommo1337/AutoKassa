using System.Windows;
using System.Windows.Media.Effects;

namespace AutoKassa.Helpers
{
    /// <summary>
    /// Helper для применения эффекта размытия к окнам
    /// </summary>
    public static class BlurHelper
    {
        /// <summary>
        /// Применить размытие к окну
        /// </summary>
        public static void ApplyBlur(Window window, double radius = 10)
        {
            if (window?.Content is UIElement content)
            {
                content.Effect = new BlurEffect
                {
                    Radius = radius,
                    KernelType = KernelType.Gaussian
                };
            }
        }

        /// <summary>
        /// Убрать размытие с окна
        /// </summary>
        public static void RemoveBlur(Window window)
        {
            if (window?.Content is UIElement content)
            {
                content.Effect = null;
            }
        }
    }
}