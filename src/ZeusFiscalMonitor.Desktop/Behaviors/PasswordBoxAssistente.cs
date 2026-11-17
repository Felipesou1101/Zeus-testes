using System.Windows;
using System.Windows.Controls;

namespace ZeusFiscalMonitor.Desktop.Behaviors;

/// <summary>
/// PasswordBox.Password não é DependencyProperty (por segurança). Esta attached property
/// permite o binding MVVM sem code-behind na View.
/// </summary>
public static class PasswordBoxAssistente
{
    public static readonly DependencyProperty SenhaProperty = DependencyProperty.RegisterAttached(
        "Senha", typeof(string), typeof(PasswordBoxAssistente),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, AoMudarSenha));

    private static readonly DependencyProperty AtualizandoProperty =
        DependencyProperty.RegisterAttached("Atualizando", typeof(bool), typeof(PasswordBoxAssistente));

    public static string GetSenha(DependencyObject d) => (string)d.GetValue(SenhaProperty);
    public static void SetSenha(DependencyObject d, string value) => d.SetValue(SenhaProperty, value);

    private static void AoMudarSenha(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox caixa)
            return;

        caixa.PasswordChanged -= AoDigitar;
        if (!(bool)caixa.GetValue(AtualizandoProperty))
            caixa.Password = e.NewValue as string ?? string.Empty;
        caixa.PasswordChanged += AoDigitar;
    }

    private static void AoDigitar(object sender, RoutedEventArgs e)
    {
        var caixa = (PasswordBox)sender;
        caixa.SetValue(AtualizandoProperty, true);
        SetSenha(caixa, caixa.Password);
        caixa.SetValue(AtualizandoProperty, false);
    }
}
