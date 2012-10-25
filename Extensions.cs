using System;
using System.Windows;
using System.Windows.Input;

namespace PathSharp
{
    public static class Extensions
    {
        public static void AddShortcut(this UIElement element, InputGesture gesture, Action action)
        {
            element.CommandBindings.Add(new CommandBinding(new RoutedCommand() { InputGestures = { gesture } }, (o, e) => action()));
        }
    }
}
