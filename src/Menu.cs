// Uygulamanin koyu temasina uygun acilir menu: saat yanindaki simgenin sag tik menusu ve dil secimi.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;

namespace Ambiance
{
    partial class App
    {
        const string MenuXaml = @"
<ContextMenu xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
             xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
             HasDropShadow='True' FontFamily='Segoe UI Variable Text, Segoe UI' FontSize='13'
             UseLayoutRounding='True' TextOptions.TextFormattingMode='Display' MinWidth='240'>
  <ContextMenu.Template>
    <ControlTemplate TargetType='ContextMenu'>
      <Border Margin='6,4,14,14' Background='#FF2A2A30' BorderBrush='#26FFFFFF' BorderThickness='1' CornerRadius='8' Padding='4'>
        <Border.Effect><DropShadowEffect BlurRadius='16' ShadowDepth='4' Direction='270' Opacity='0.5'/></Border.Effect>
        <StackPanel IsItemsHost='True'/>
      </Border>
    </ControlTemplate>
  </ContextMenu.Template>
  <ContextMenu.Resources>
    <Style TargetType='MenuItem'>
      <Setter Property='Foreground' Value='#FFFFFFFF'/>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='MenuItem'>
            <Border x:Name='bg' CornerRadius='5' Background='Transparent' Height='36' Margin='0,1'>
              <Grid>
                <Border x:Name='pill' Width='3' Height='16' CornerRadius='1.5' HorizontalAlignment='Left' Visibility='Collapsed'>
                  <Border.Background>
                    <LinearGradientBrush StartPoint='0,0' EndPoint='1,1'>
                      <GradientStop Color='#FFFF8A3D' Offset='0'/>
                      <GradientStop Color='#FFC04BFF' Offset='1'/>
                    </LinearGradientBrush>
                  </Border.Background>
                </Border>
                <DockPanel Margin='12,0,14,0'>
                  <ContentPresenter ContentSource='Icon' Width='28' VerticalAlignment='Center'
                                    TextElement.FontFamily='Segoe Fluent Icons, Segoe MDL2 Assets' TextElement.FontSize='15'/>
                  <TextBlock DockPanel.Dock='Right' Text='{TemplateBinding InputGestureText}' Margin='24,0,0,0'
                             Foreground='#78FFFFFF' FontSize='12' VerticalAlignment='Center'/>
                  <ContentPresenter ContentSource='Header' VerticalAlignment='Center' RecognizesAccessKey='False'/>
                </DockPanel>
              </Grid>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsHighlighted' Value='True'>
                <Setter TargetName='bg' Property='Background' Value='#16FFFFFF'/>
              </Trigger>
              <Trigger Property='IsChecked' Value='True'>
                <Setter TargetName='bg' Property='Background' Value='#10FFFFFF'/>
                <Setter TargetName='pill' Property='Visibility' Value='Visible'/>
              </Trigger>
              <Trigger Property='IsEnabled' Value='False'>
                <Setter Property='Opacity' Value='0.4'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style x:Key='Sep' TargetType='Separator'>
      <Setter Property='Margin' Value='0'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Separator'>
            <Border Height='1' Background='#16FFFFFF' Margin='6,4'/>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </ContextMenu.Resources>
</ContextMenu>";

        static ContextMenu NewMenu(bool fromTray)
        {
            var m = (ContextMenu)XamlReader.Parse(MenuXaml);
            // simge menusu odaksiz pencereden acilir: disariya tiklaninca kapanabilmesi icin one getir
            if (fromTray) m.Opened += delegate
            {
                var src = PresentationSource.FromVisual(m) as HwndSource;
                if (src != null) N.SetForegroundWindow(src.Handle);
            };
            return m;
        }

        // Menu icindeki ayiraclara WPF kendi stilini verir; bizimkini acikca atamak gerekiyor
        static void Sep(ContextMenu m)
        {
            var s = new Separator();
            s.Style = (Style)m.Resources["Sep"];
            m.Items.Add(s);
        }

        static MenuItem Item(ContextMenu m, string text, string glyph, Action onClick)
        {
            var it = new MenuItem();
            it.Header = text;
            if (glyph != null) it.Icon = glyph;
            it.Click += delegate { onClick(); };
            m.Items.Add(it);
            return it;
        }

        // ---------------- saat yanindaki simgenin menusu ----------------
        static void ShowTrayMenu()
        {
            var m = NewMenu(true);
            var open = Item(m, L("Ambiance Ultrawide'ı aç"), "\uE80F", ShowSettings);
            open.FontWeight = FontWeights.SemiBold;
            var tog = Item(m, L(active ? "Durdur" : "Başlat"), active ? "\uE71A" : "\uE768", Toggle);
            tog.InputGestureText = "Ctrl+Alt+A";
            Sep(m);
            Item(m, L("Çıkış"), "\uE7E8", delegate { Deactivate(); Application.Current.Shutdown(); });
            m.Placement = PlacementMode.MousePoint;
            m.IsOpen = true;
        }

        // ---------------- dil secimi ----------------
        static readonly string[] LangNames = { "Türkçe", "English", "Español", "Português", "Français", "Deutsch", "Русский", "中文", "日本語", "한국어", "Italiano", "Polski" };

        static string LangLabel()
        {
            return langPref == "" ? L("Otomatik (Windows dili)") : LangNames[LangIdx];
        }

        static void ShowLangMenu(FrameworkElement anchor)
        {
            var m = NewMenu(false);
            var auto = Item(m, L("Otomatik (Windows dili)"), "\uE770", delegate { SetLanguage(""); });
            auto.IsChecked = langPref == "";
            Sep(m);
            for (int i = 0; i < Langs.Length; i++)
            {
                string code = Langs[i];
                var it = Item(m, LangNames[i], null, delegate { SetLanguage(code); });
                it.IsChecked = langPref == code;
            }
            m.PlacementTarget = anchor;
            m.Placement = PlacementMode.Bottom;
            m.HorizontalOffset = -6;
            m.IsOpen = true;
        }

        // Dili degistirir ve ayar penceresini yeni dilde hemen yeniden kurar
        static void SetLanguage(string code)
        {
            if (code == langPref) return;
            langPref = code;
            LangIdx = PickLang();
            table = null;
            SaveSettings();
            if (ui == null) return;
            var old = ui;
            double l = old.Left, t = old.Top, w = old.Width, h = old.Height;
            var state = old.WindowState;
            rebuildingUi = true;
            try
            {
                ui = null;
                BuildUi();
                ui.WindowStartupLocation = WindowStartupLocation.Manual;
                ui.Left = l; ui.Top = t; ui.Width = w; ui.Height = h;
                F<RadioButton>("NavGeneral").IsChecked = true;
                ShowSettings();
                ui.WindowState = state;
                old.Close();
            }
            finally { rebuildingUi = false; }
        }
    }
}
