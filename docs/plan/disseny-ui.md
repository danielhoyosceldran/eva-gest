# Decisions de disseny UI

## 1. Per a qui es dissenya

| | |
|---|---|
| **Qui** | Una barbera sense coneixements informàtics, i possiblement en Marco |
| **On** | Dreta al mostrador, en un PC amb Windows, entre client i client |
| **Com** | Cops d'ull ràpids de 3–5 segons, amb les mans ocupades i pressa |
| **Què necessita saber** | Qui ve ara, quant s'ha cobrat avui, i què queda a la caixa |

Això mana per damunt de qualsevol consideració estètica: **el disseny ha de ser llegible d'un cop d'ull i impossible de tocar per error.** No és una web de màrqueting; és una eina de treball que es fa servir cada dia.

---

## 2. Pla de disseny

### 2.1. Direcció visual

El senyal visual universal d'una barberia és el **pal de barber: blanc, vermell i blau**. És el vocabulari que la clienta reconeix, i la direcció surt d'aquí.

La disciplina és aquesta: **la superfície de treball es manté neutra i tranquil·la; el caràcter viu a la navegació lateral i en un únic accent blau.** Les taules de dades, els formularis i les xifres no es decoren. Tot el pressupost estètic es gasta en un sol lloc.

El repte és que el vermell i el verd ja tenen significat funcional a l'aplicació (cita cancel·lada i cita realitzada). La solució és **separar per zones**:

| Color | On viu | On NO apareix mai |
|---|---|---|
| **Blau** | Accent primari: botons d'acció, focus, element de navegació actiu | — |
| **Blanc** | Superfícies de dades: targetes, files de taula, camps | — |
| **Vermell** | Franja d'identitat a la capçalera lateral, i accions destructives | Enlloc més de la zona de dades |

La franja del pal viu a la capçalera de la navegació, físicament separada de qualsevol taula. Allà el vermell no pot confondre's amb un estat perquè no hi ha cap dada a prop.

### 2.2. Paleta

Sis valors base. Tots els contrastos estan calculats i verificats (secció 7).

| Nom | Hex | Ús |
|---|---|---|
| `Navy` | `#16202B` | Fons de la navegació lateral |
| `Ink` | `#1B2430` | Text principal, xifres |
| `InkMuted` | `#5A6673` | Etiquetes, text secundari |
| `Paper` | `#EFF1F3` | Fons de l'àrea de contingut |
| `Surface` | `#FFFFFF` | Targetes, files de taula, camps |
| `Blau` | `#1E40AF` | Accent únic: acció primària, focus, subratllat actiu |

Més dos colors de suport:

- `BlauClar` `#7FA0F0`, per a l'element actiu sobre el fons fosc de la barra lateral
- `VermellPal` `#C8102E`, **exclusivament** per a la franja d'identitat de la capçalera lateral

`Line` `#D5DBE1` per a vores i separadors.

### 2.3. Tipografia

**Una sola família:** Segoe UI Variable a Windows 11, amb caiguda a Segoe UI a Windows 10. Cap font incrustada, cap descàrrega.

La jerarquia la porten **la mida i el pes**, no famílies diferents. En una eina de dades, barrejar tipografies afegeix soroll sense afegir informació.

El moment tipogràfic del disseny és **les xifres de diners**: grans, amb xifres tabulars perquè les columnes s'alineïn, i amb el balanç del dia com a element més gran de tota l'aplicació.

### 2.4. Concepte de disposició

```
┌────────────┬───────────────────────────────────────────────┐
│███         │  Dimecres, 9 de setembre                      │   ← franja del pal
│▏ Inici     │                                               │
│  Agenda    │  ┌─────────────────────────────────────────┐   │
│  Clients   │  │                                         │   │
│  Treball.  │  │            BALANÇ DEL DIA               │   │
│  Catàleg   │  │              35,00 €                    │   │  ← única xifra gran
│  Vendes    │  │                                         │   │
│  Caixa     │  └─────────────────────────────────────────┘   │
│  Informes  │                                               │
│  Config.   │  Cites 5   Vendes 2   Cobrat 25,00 €          │  ← secundari, sense caixes
│            │  ─────────────────────────────────────────    │
│  ? Ajuda   │                                               │
└────────────┴───────────────────────────────────────────────┘
   ███ = franja blanc/vermell/blau a la capçalera (identitat)
   ▏   = subratllat blau a l'element actiu
```

**Alineació:** tot alineat a l'esquerra, excepte les columnes d'imports, que van **alineades a la dreta amb xifres tabulars** perquè els dígits quedin en columna i els errors es vegin.

### 2.5. Principis

1. **Una sola xifra gran per pantalla.** A l'Inici és el balanç; a Caixa és el balanç del període. La resta són dades de suport i no competeixen.
2. **Les targetes no són uniformes.** La xifra important va dins d'una superfície amb una regla blava a sobre; les secundàries són text sobre el fons, separades per una línia fina. Res d'ombres iguals a tot arreu.
3. **L'estat sempre porta text.** Cap informació depèn només del color (RNF-05).
4. **Els botons destructius no s'assemblen mai als altres** i sempre estan separats físicament.
5. **Cap moviment que no respongui a una acció.** Sense animacions d'entrada ni transicions decoratives.

---

## 3. Autocrítica: què he descartat

El primer instint per a un projecte així seria un fons crema càlid, una serif de contrast alt i un accent terracota. **És exactament el patró que produiria per a qualsevol altre encàrrec**, no una decisió presa per aquesta barberia. Descartat.

| Descartat | Per què |
|---|---|
| Fons crema càlid + serif + terracota | És el patró per defecte, no una tria. I una serif en taules de dades perjudica la llegibilitat |
| ~~Vermell/blanc/blau del pal de barber~~ | **Descartat a la primera versió i després recuperat.** El motiu era real (el vermell ja significa "cancel·lada"), però la conclusió era mandrosa: en lloc de renunciar al senyal visual més reconeixible d'una barberia, la solució correcta és **separar per zones**. El blau porta tot el pes com a accent i el vermell es limita a la franja d'identitat, lluny de qualsevol dada |
| Targetes idèntiques amb la mateixa ombra | Aplana la jerarquia justament on cal que sigui evident quina xifra importa |
| Etiquetes en MAJÚSCULES espaiades | Es llegeixen pitjor d'un cop d'ull, que és tot el que fa aquesta usuària |
| Mode fosc | Es va llistar com a "pijada" i no es va triar. Ús diürn, un sol PC |
| Icones a tot arreu | Sense text, una icona és una endevinalla per a algú sense hàbit informàtic. Text sempre; icona només com a acompanyament |

---

## 4. `ResourceDictionary`: fitxers

```
Resources/
├── Colors.xaml        # Color i SolidColorBrush
├── Typography.xaml    # Mides, pesos, famílies
├── Metrics.xaml       # Espaiats, alçades, radis
├── Controls.xaml      # Estils de Button, TextBox, DataGrid...
└── Sons/
    └── cobrar.wav
```

Es combinen a `App.xaml`:

```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="Resources/Colors.xaml" />
            <ResourceDictionary Source="Resources/Typography.xaml" />
            <ResourceDictionary Source="Resources/Metrics.xaml" />
            <ResourceDictionary Source="Resources/Controls.xaml" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

**L'ordre importa:** `Controls.xaml` va l'últim perquè fa servir els recursos dels tres anteriors.

---

### 4.1. `Colors.xaml`

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- Base palette -->
    <Color x:Key="NavyColor">#16202B</Color>
    <Color x:Key="InkColor">#1B2430</Color>
    <Color x:Key="InkMutedColor">#5A6673</Color>
    <Color x:Key="PaperColor">#EFF1F3</Color>
    <Color x:Key="SurfaceColor">#FFFFFF</Color>
    <Color x:Key="LineColor">#D5DBE1</Color>

    <!-- Single accent, from the blue of the barber pole.
         Dark enough for white text on top (8.72:1) -->
    <Color x:Key="BlauColor">#1E40AF</Color>
    <Color x:Key="BlauClarColor">#7FA0F0</Color>

    <!-- Barber pole red. Identity only: the stripe in the sidebar header.
         Never used for data, so it cannot be mistaken for a status. -->
    <Color x:Key="VermellPalColor">#C8102E</Color>

    <SolidColorBrush x:Key="NavyBrush" Color="{StaticResource NavyColor}" />
    <SolidColorBrush x:Key="InkBrush" Color="{StaticResource InkColor}" />
    <SolidColorBrush x:Key="InkMutedBrush" Color="{StaticResource InkMutedColor}" />
    <SolidColorBrush x:Key="PaperBrush" Color="{StaticResource PaperColor}" />
    <SolidColorBrush x:Key="SurfaceBrush" Color="{StaticResource SurfaceColor}" />
    <SolidColorBrush x:Key="LineBrush" Color="{StaticResource LineColor}" />
    <SolidColorBrush x:Key="BlauBrush" Color="{StaticResource BlauColor}" />
    <SolidColorBrush x:Key="BlauClarBrush" Color="{StaticResource BlauClarColor}" />
    <SolidColorBrush x:Key="VermellPalBrush" Color="{StaticResource VermellPalColor}" />

    <!-- Sidebar text -->
    <SolidColorBrush x:Key="NavTextBrush" Color="#C3CBD4" />
    <SolidColorBrush x:Key="NavTextActiveBrush" Color="#FFFFFF" />
    <SolidColorBrush x:Key="NavHoverBrush" Color="#1F2C3A" />

    <!-- Status badges: tinted background + dark text. Never colour alone: the badge
         always shows its label as text, so meaning survives colour blindness. -->
    <SolidColorBrush x:Key="PendentBgBrush" Color="#FDF0D5" />
    <SolidColorBrush x:Key="PendentFgBrush" Color="#8A5A00" />
    <SolidColorBrush x:Key="RealitzadaBgBrush" Color="#E3F3E6" />
    <SolidColorBrush x:Key="RealitzadaFgBrush" Color="#1F6B3A" />
    <SolidColorBrush x:Key="CancelladaBgBrush" Color="#FBE9E7" />
    <SolidColorBrush x:Key="CancelladaFgBrush" Color="#A32B1C" />
    <SolidColorBrush x:Key="NeutralBgBrush" Color="#ECEEF0" />
    <SolidColorBrush x:Key="NeutralFgBrush" Color="#455160" />

    <!-- Destructive actions: outlined, never filled, and never next to Save -->
    <SolidColorBrush x:Key="DangerBrush" Color="#A32B1C" />
    <SolidColorBrush x:Key="DangerBgBrush" Color="#FBE9E7" />

    <!-- Inline warnings inside dialogs (overlap, out of hours, guest client) -->
    <SolidColorBrush x:Key="AvisBgBrush" Color="#FDF0D5" />
    <SolidColorBrush x:Key="AvisBorderBrush" Color="#E3C77A" />
</ResourceDictionary>
```

---

### 4.2. `Typography.xaml`

**Sobre les unitats:** `FontSize` a WPF s'expressa en píxels independents del dispositiu (1/96 de polzada), **no en punts**. La conversió és `pt = DIP × 0,75`. Aquesta és la font d'un error fàcil: escriure `FontSize="14"` no dona 14 pt sinó 10,5 pt, massa petit per a aquesta usuària.

| Token | DIP | ≈ pt | Ús |
|---|---|---|---|
| `FontSizeSmall` | 14 | 10,5 | Notes al peu, unitats |
| `FontSizeBody` | 16 | 12 | Taules, text general |
| `FontSizeInput` | 18 | 13,5 | Camps de formulari i etiquetes |
| `FontSizeSubhead` | 20 | 15 | Títols de bloc |
| `FontSizeTitle` | 26 | 19,5 | Títol de pàgina |
| `FontSizeFigure` | 30 | 22,5 | Xifres de les targetes de resum |
| `FontSizeHero` | 44 | 33 | La xifra gran única per pantalla |

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:sys="clr-namespace:System;assembly=System.Runtime">

    <!-- Segoe UI Variable ships with Windows 11; Segoe UI is the Windows 10 fallback -->
    <FontFamily x:Key="FontUi">Segoe UI Variable Text, Segoe UI</FontFamily>
    <FontFamily x:Key="FontDisplay">Segoe UI Variable Display, Segoe UI</FontFamily>

    <sys:Double x:Key="FontSizeSmall">14</sys:Double>
    <sys:Double x:Key="FontSizeBody">16</sys:Double>
    <sys:Double x:Key="FontSizeInput">18</sys:Double>
    <sys:Double x:Key="FontSizeSubhead">20</sys:Double>
    <sys:Double x:Key="FontSizeTitle">26</sys:Double>
    <sys:Double x:Key="FontSizeFigure">30</sys:Double>
    <sys:Double x:Key="FontSizeHero">44</sys:Double>

    <!-- Page title -->
    <Style x:Key="TitolPagina" TargetType="TextBlock">
        <Setter Property="FontFamily" Value="{StaticResource FontDisplay}" />
        <Setter Property="FontSize" Value="{StaticResource FontSizeTitle}" />
        <Setter Property="FontWeight" Value="SemiBold" />
        <Setter Property="Foreground" Value="{StaticResource InkBrush}" />
        <Setter Property="Margin" Value="0,0,0,20" />
    </Style>

    <!-- Block heading -->
    <Style x:Key="Subtitol" TargetType="TextBlock">
        <Setter Property="FontSize" Value="{StaticResource FontSizeSubhead}" />
        <Setter Property="FontWeight" Value="SemiBold" />
        <Setter Property="Foreground" Value="{StaticResource InkBrush}" />
        <Setter Property="Margin" Value="0,20,0,10" />
    </Style>

    <!-- Small label above a figure. Sentence case, never spaced capitals. -->
    <Style x:Key="EtiquetaDada" TargetType="TextBlock">
        <Setter Property="FontSize" Value="{StaticResource FontSizeSmall}" />
        <Setter Property="Foreground" Value="{StaticResource InkMutedBrush}" />
        <Setter Property="Margin" Value="0,0,0,4" />
    </Style>

    <!-- Money and counts. Tabular figures keep digits in a column so a wrong
         amount is visible at a glance instead of hiding in ragged alignment. -->
    <Style x:Key="Xifra" TargetType="TextBlock">
        <Setter Property="FontFamily" Value="{StaticResource FontDisplay}" />
        <Setter Property="FontSize" Value="{StaticResource FontSizeFigure}" />
        <Setter Property="FontWeight" Value="SemiBold" />
        <Setter Property="Foreground" Value="{StaticResource InkBrush}" />
        <Setter Property="Typography.NumeralAlignment" Value="Tabular" />
    </Style>

    <!-- The single large figure per screen -->
    <Style x:Key="XifraHero" TargetType="TextBlock" BasedOn="{StaticResource Xifra}">
        <Setter Property="FontSize" Value="{StaticResource FontSizeHero}" />
        <Setter Property="FontWeight" Value="Bold" />
    </Style>

    <!-- Amounts inside tables: right-aligned and tabular -->
    <Style x:Key="XifraTaula" TargetType="TextBlock">
        <Setter Property="FontSize" Value="{StaticResource FontSizeBody}" />
        <Setter Property="Foreground" Value="{StaticResource InkBrush}" />
        <Setter Property="TextAlignment" Value="Right" />
        <Setter Property="Typography.NumeralAlignment" Value="Tabular" />
    </Style>
</ResourceDictionary>
```

---

### 4.3. `Metrics.xaml`

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:sys="clr-namespace:System;assembly=System.Runtime">

    <!-- Spacing scale: 4 / 8 / 12 / 20 / 32 -->
    <Thickness x:Key="PadTight">8</Thickness>
    <Thickness x:Key="PadBase">12</Thickness>
    <Thickness x:Key="PadCard">20</Thickness>
    <Thickness x:Key="PadPagina">32,24,32,24</Thickness>

    <!-- Touch targets: generous, because this is used in a hurry (RNF-03) -->
    <sys:Double x:Key="AlcadaBoto">44</sys:Double>
    <sys:Double x:Key="AlcadaBotoGran">64</sys:Double>
    <sys:Double x:Key="AlcadaCamp">40</sys:Double>
    <sys:Double x:Key="AlcadaFila">44</sys:Double>
    <sys:Double x:Key="AmpladaNav">200</sys:Double>

    <!-- Two radii only: 4 for controls, 8 for surfaces. Not one value everywhere. -->
    <CornerRadius x:Key="RadiControl">4</CornerRadius>
    <CornerRadius x:Key="RadiSuperficie">8</CornerRadius>

    <sys:Double x:Key="GruixRegla">3</sys:Double>
</ResourceDictionary>
```

---

### 4.4. `Controls.xaml` — extractes clau

**Botó primari** (una acció principal per diàleg: `Guardar`, `Cobrar`):

```xml
<Style x:Key="BotoPrimari" TargetType="Button">
    <Setter Property="Background" Value="{StaticResource BlauBrush}" />
    <Setter Property="Foreground" Value="White" />
    <Setter Property="FontSize" Value="{StaticResource FontSizeInput}" />
    <Setter Property="FontWeight" Value="SemiBold" />
    <Setter Property="MinHeight" Value="{StaticResource AlcadaBoto}" />
    <Setter Property="Padding" Value="20,0" />
    <Setter Property="Cursor" Value="Hand" />
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="Button">
                <Border x:Name="Fons" Background="{TemplateBinding Background}"
                        CornerRadius="{StaticResource RadiControl}">
                    <ContentPresenter HorizontalAlignment="Center"
                                      VerticalAlignment="Center"
                                      Margin="{TemplateBinding Padding}" />
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="Fons" Property="Background" Value="#7A5819" />
                    </Trigger>
                    <Trigger Property="IsKeyboardFocused" Value="True">
                        <Setter TargetName="Fons" Property="BorderBrush" Value="{StaticResource InkBrush}" />
                        <Setter TargetName="Fons" Property="BorderThickness" Value="2" />
                    </Trigger>
                    <Trigger Property="IsEnabled" Value="False">
                        <Setter TargetName="Fons" Property="Background" Value="#C9CDD2" />
                        <Setter Property="Foreground" Value="#7A8189" />
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

**Botó secundari** (`Cancel·lar`, `Editar`): mateixa geometria, contorn en lloc de farciment.

```xml
<Style x:Key="BotoSecundari" TargetType="Button" BasedOn="{StaticResource BotoPrimari}">
    <Setter Property="Background" Value="{StaticResource SurfaceBrush}" />
    <Setter Property="Foreground" Value="{StaticResource InkBrush}" />
    <Setter Property="FontWeight" Value="Normal" />
</Style>
```

**Botó destructiu** (`Eliminar`, `Anul·lar`): contorn vermell, mai farcit. Un botó vermell ple crida l'atenció i convida a prémer-lo, que és el contrari del que volem.

```xml
<Style x:Key="BotoDestructiu" TargetType="Button" BasedOn="{StaticResource BotoPrimari}">
    <Setter Property="Background" Value="{StaticResource SurfaceBrush}" />
    <Setter Property="Foreground" Value="{StaticResource DangerBrush}" />
    <Setter Property="BorderBrush" Value="{StaticResource DangerBrush}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="FontWeight" Value="Normal" />
    <!-- Always separated from the confirm button by at least 24 DIP -->
    <Setter Property="Margin" Value="24,0,0,0" />
</Style>
```

**Botó d'acció ràpida** de l'Inici: alt, ample, text llarg i clar.

```xml
<Style x:Key="BotoAccioRapida" TargetType="Button" BasedOn="{StaticResource BotoSecundari}">
    <Setter Property="MinHeight" Value="{StaticResource AlcadaBotoGran}" />
    <Setter Property="FontSize" Value="{StaticResource FontSizeSubhead}" />
    <Setter Property="BorderBrush" Value="{StaticResource LineBrush}" />
    <Setter Property="BorderThickness" Value="1" />
</Style>
```

**Camp de text:**

```xml
<Style TargetType="TextBox">
    <Setter Property="FontSize" Value="{StaticResource FontSizeInput}" />
    <Setter Property="MinHeight" Value="{StaticResource AlcadaCamp}" />
    <Setter Property="Padding" Value="10,0" />
    <Setter Property="VerticalContentAlignment" Value="Center" />
    <Setter Property="Background" Value="{StaticResource SurfaceBrush}" />
    <Setter Property="BorderBrush" Value="{StaticResource LineBrush}" />
    <Setter Property="Foreground" Value="{StaticResource InkBrush}" />
    <Style.Triggers>
        <!-- Focus is shown with the accent, 2 DIP thick: visible without being loud -->
        <Trigger Property="IsKeyboardFocused" Value="True">
            <Setter Property="BorderBrush" Value="{StaticResource BlauBrush}" />
            <Setter Property="BorderThickness" Value="2" />
        </Trigger>
    </Style.Triggers>
</Style>
```

**Superfície de la xifra gran** — l'únic lloc amb la regla blava:

```xml
<Style x:Key="TargetaHero" TargetType="Border">
    <Setter Property="Background" Value="{StaticResource SurfaceBrush}" />
    <Setter Property="CornerRadius" Value="{StaticResource RadiSuperficie}" />
    <Setter Property="Padding" Value="{StaticResource PadCard}" />
    <Setter Property="BorderBrush" Value="{StaticResource BlauBrush}" />
    <!-- Rule on the top edge only: marks importance without boxing everything in -->
    <Setter Property="BorderThickness" Value="0,3,0,0" />
</Style>
```

**Superfície secundària** — sense regla, sense ombra:

```xml
<Style x:Key="TargetaDada" TargetType="Border">
    <Setter Property="Background" Value="{StaticResource SurfaceBrush}" />
    <Setter Property="CornerRadius" Value="{StaticResource RadiSuperficie}" />
    <Setter Property="Padding" Value="{StaticResource PadCard}" />
    <Setter Property="BorderBrush" Value="{StaticResource LineBrush}" />
    <Setter Property="BorderThickness" Value="1" />
</Style>
```

**Insígnia d'estat.** El color canvia amb un `DataTrigger`, però **el text sempre hi és**:

```xml
<Style x:Key="Insignia" TargetType="Border">
    <Setter Property="CornerRadius" Value="{StaticResource RadiControl}" />
    <Setter Property="Padding" Value="8,3" />
    <Setter Property="HorizontalAlignment" Value="Left" />
    <Setter Property="Background" Value="{StaticResource NeutralBgBrush}" />
</Style>
```

**Element de navegació lateral.** El subratllat blau a l'esquerra marca l'actiu; no depèn només del color perquè també canvia el pes del text:

```xml
<Style x:Key="ItemNav" TargetType="RadioButton">
    <Setter Property="Foreground" Value="{StaticResource NavTextBrush}" />
    <Setter Property="FontSize" Value="{StaticResource FontSizeInput}" />
    <Setter Property="MinHeight" Value="{StaticResource AlcadaBoto}" />
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="RadioButton">
                <Grid x:Name="Arrel" Background="Transparent">
                    <Border x:Name="Marca" Width="3" HorizontalAlignment="Left"
                            Background="Transparent" />
                    <ContentPresenter Margin="20,0,12,0" VerticalAlignment="Center" />
                </Grid>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="Arrel" Property="Background"
                                Value="{StaticResource NavHoverBrush}" />
                    </Trigger>
                    <Trigger Property="IsChecked" Value="True">
                        <Setter TargetName="Marca" Property="Background"
                                Value="{StaticResource BlauClarBrush}" />
                        <Setter Property="Foreground" Value="{StaticResource NavTextActiveBrush}" />
                        <Setter Property="FontWeight" Value="SemiBold" />
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

**Franja d'identitat del pal.** Va a la capçalera de la barra lateral, sobre el nom de la barberia. És l'únic lloc de tota l'aplicació on apareix el vermell decoratiu:

```xml
<!-- Barber pole stripe: the one decorative element in the whole app.
     Lives in the sidebar header, far from any data, so the red can never
     be confused with the "cancelled" status colour. -->
<Style x:Key="FranjaPal" TargetType="Border">
    <Setter Property="Height" Value="6" />
    <Setter Property="Background">
        <Setter.Value>
            <LinearGradientBrush StartPoint="0,0" EndPoint="1,0">
                <GradientStop Color="{StaticResource VermellPalColor}" Offset="0.00" />
                <GradientStop Color="{StaticResource VermellPalColor}" Offset="0.33" />
                <GradientStop Color="#FFFFFF"                          Offset="0.33" />
                <GradientStop Color="#FFFFFF"                          Offset="0.66" />
                <GradientStop Color="{StaticResource BlauColor}"        Offset="0.66" />
                <GradientStop Color="{StaticResource BlauColor}"        Offset="1.00" />
            </LinearGradientBrush>
        </Setter.Value>
    </Setter>
</Style>
```

Tres bandes netes, sense degradat suau i sense animació de gir. Un pal de barber animat seria una distracció permanent en una eina que s'obre cinquanta vegades al dia.

---

## 5. Colors de treballadora

Els assigna l'aplicació en crear la treballadora, i es poden canviar. Sis valors, **triats per no col·lidir amb els colors semàntics d'estat** (verd = realitzada, vermell = cancel·lada, ambre = pendent):

| Nom | Hex | Contrast sobre blanc |
|---|---|---|
| Verd blau | `#0F766E` | 5,47 |
| Magenta | `#A21CAF` | 6,32 |
| Prunya | `#7E3F8F` | 7,01 |
| Oliva | `#4D7C0F` | 4,99 |
| Ocre | `#92400E` | 7,09 |
| Grafit | `#374151` | 10,31 |

**Cap d'aquests colors és blau.** Ara que el blau és l'accent de l'aplicació (botons, focus, element actiu), un punt blau al costat d'un nom es llegiria com un element interactiu en lloc d'una identitat. Per això la paleta de treballadores evita del tot la família del blau.

Apareixen com a **punt de color acompanyat sempre del nom**, mai com a única font d'informació.

---

## 6. Moviment i so

**Moviment.** Només com a resposta a una acció, i mínim:

| Moment | Efecte |
|---|---|
| Obrir un diàleg | Aparició en 120 ms, sense desplaçament |
| Passar el ratolí per un botó | Canvi de fons en 80 ms |
| Confirmar una venda | Res visual: el so ja fa la feina |

Cap animació d'entrada de pàgina, cap transició entre seccions. En una eina que s'obre cinquanta vegades al dia, l'animació és una nosa.

**So** (RF-22). Un únic `cobrar.wav`, curt (menys de 200 ms), discret, i desactivable des de la configuració. Un so que s'escolta desenes de vegades al dia ha de ser gairebé imperceptible; res de campanes de caixa registradora.

---

## 7. Contrastos verificats

Tots aquests valors s'han calculat, no estimat. El mínim de WCAG AA per a text normal és **4,5:1**.

| Combinació | Contrast |
|---|---|
| `Ink` sobre `Surface` | 15,65 |
| `Ink` sobre `Paper` | 13,82 |
| `InkMuted` sobre `Surface` | 5,86 |
| `InkMuted` sobre `Paper` | 5,18 |
| Blanc sobre `Navy` | 16,46 |
| Text de navegació sobre `Navy` | 10,05 |
| `BlauClar` sobre `Navy` | 7,72 |
| `VermellPal` sobre `Surface` | 5,88 |
| Blanc sobre `Blau` (botó primari) | 8,72 |
| Insígnia Pendent | 5,25 |
| Insígnia Realitzada | 5,66 |
| Insígnia Cancel·lada | 6,13 |
| Insígnia neutra | 6,95 |

**Nota sobre el vermell del pal.** Sobre blanc dona 5,88:1, de sobres. Però sobre el fons fosc de la barra lateral només arriba a 2,80:1, insuficient per a text. Per això s'usa **només com a franja de color**, mai per a text ni per a res que hagi de llegir-se.

**Nota sobre els dos vermells.** El vermell d'identitat (`#C8102E`) i el de perill (`#A32B1C`) són tons propers. No es confonen perquè **no comparteixen mai la mateixa zona**: el primer viu a la capçalera de la barra lateral, el segon només dins de diàlegs i botons destructius.

---

## 8. Terra de qualitat

| Requisit | Concreció |
|---|---|
| Focus de teclat visible | Vora de 2 DIP en color blau a tots els controls. Mai `FocusVisualStyle="{x:Null}"` |
| Ordre de tabulació | Segueix l'ordre de lectura de cada diàleg; `IsDefault` al botó primari i `IsCancel` al de cancel·lar |
| Res depèn només del color | Estats amb text, treballadores amb nom, actiu/inactiu amb paraula |
| Alçada mínima de clic | 40 DIP als camps, 44 als botons, 64 a les accions ràpides |
| Xifres alineades | `Typography.NumeralAlignment="Tabular"` a tot import |
| Escalat de Windows | Provar al 100%, 125% i 150%; res amb amplada fixa en píxels excepte la navegació |

---

## 9. Veu de la interfície

Els textos són contingut de disseny, no decoració.

| Norma | Sí | No |
|---|---|---|
| Verbs concrets al botó | `Cobrar`, `Fer còpia ara` | `Enviar`, `Acceptar` |
| El mateix nom a tot el flux | Botó `Cobrar` → missatge "Venda cobrada" | Botó `Cobrar` → "Registre creat" |
| Frase normal, no majúscules | `Balanç del dia` | `BALANÇ DEL DIA` |
| Els errors diuen què fer | "Cal indicar el nom del client per guardar la cita." | "Error de validació" |
| Les pantalles buides conviden | "Avui no tens cap cita apuntada." + botó | Una taula buida |
| Sense disculpes ni tecnicismes | "No s'ha pogut guardar el client." | "Ho sentim, SQLiteException…" |

---

## 10. Reconciliació amb `pantalles.md`

El document de pantalles deia "mida de lletra base 14 pt; xifres destacades 22 pt". Interpretat literalment com a `FontSize="14"` a WPF, això donaria **10,5 pt**, massa petit per a aquesta usuària.

Els tokens d'aquest document substitueixen aquelles xifres soltes: `FontSizeBody` (16 DIP ≈ 12 pt) per a taules i `FontSizeFigure` (30 DIP ≈ 22,5 pt) per a les xifres destacades. `pantalles.md` s'actualitza per referenciar els noms dels tokens en lloc de mides literals.

---

## 11. Documentació completa

Amb aquest document es tanca la fase de documentació:

| Document | Contingut |
|---|---|
| `requeriments-barberia-v2.md` | 28 requisits funcionals, no funcionals i decisions tècniques |
| `propuesta-cliente-barberia.md` | Proposta per a la clienta, en llenguatge planer |
| `resumen-rapido-clienta.md` | Resum de lectura ràpida |
| `stack-arquitectura.md` | Tecnologies i arquitectura en capes |
| `esquema-bbdd.md` | 13 taules, índexs, claus i restriccions |
| `diagrama-er.mermaid` | Diagrama entitat-relació |
| `models-domini.md` | Entitats C#, enums i calculadors |
| `pantalles.md` | 9 pàgines i 9 diàlegs |
| `casos-us.md` | 14 diagrames de flux |
| `capa-mvvm.md` | 11 serveis, 26 ViewModels, `DbContext` |
| `disseny-ui.md` | Aquest document |
| `pla-proves.md` | 154 proves automàtiques a implementar |
| `setup-projecte.md` | Muntatge a Visual Studio |
| `mockup-barberia.html` | Prototip navegable |

**Següent pas:** crear el projecte a Visual Studio (WPF Application, .NET 10), afegir els paquets `Microsoft.EntityFrameworkCore.Sqlite`, `CommunityToolkit.Mvvm` i `Serilog`, i generar la migració inicial.
