dpl2\Views\PlayersPage.xaml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage
    x:Class="Wdpl2.Views.PlayersPage"
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:ctrls="clr-namespace:Wdpl2.Views.Controls"
    Title="Players">

    <Grid ColumnDefinitions="2*,3*" RowDefinitions="Auto,*">
        <!-- Header -->
        <Grid Grid.ColumnSpan="2" Padding="12">
            <Label Text="Manage Players" FontSize="20" FontAttributes="Bold" />
        </Grid>

        <!-- Left: search + list -->
        <VerticalStackLayout Grid.Row="1" Padding="12" Spacing="8">
            <Entry x:Name="SearchEntry" Placeholder="Search players..." />
            <CollectionView x:Name="PlayersList" SelectionMode="Single">
                <CollectionView.ItemTemplate>
                    <DataTemplate>
                        <Grid Padding="8" ColumnDefinitions="Auto,*,Auto">
                            <Border
                                BackgroundColor="#3B82F6"
                                StrokeThickness="0"
                                WidthRequest="32"
                                HeightRequest="32"
                                StrokeShape="RoundRectangle 16">
                                <Label
                                    Text="{Binding Initials}"
                                    TextColor="White"
                                    FontAttributes="Bold"
                                    HorizontalOptions="Center"
                                    VerticalOptions="Center" />
                            </Border>
                            <Label Grid.Column="1" Text="{Binding FullName}" FontSize="16" Margin="8,0,0,0" VerticalOptions="Center" />
                            <Label Grid.Column="2" Text="{Binding TeamLabel}" FontSize="12" TextColor="#666" VerticalOptions="Center" />
                        </Grid>
                    </DataTemplate>
                </CollectionView.ItemTemplate>
            </CollectionView>
        </VerticalStackLayout>

        <!-- Right: editor -->
        <ScrollView Grid.Row="1" Grid.Column="1">
            <VerticalStackLayout Padding="12" Spacing="8">
                <Label Text="First Name" />
                <Entry x:Name="FirstNameEntry" />

                <Label Text="Last Name" />
                <Entry x:Name="LastNameEntry" />

                <Label Text="Team" />
                <Picker x:Name="TeamPicker" ItemDisplayBinding="{Binding Name}" />

                <Label Text="Notes" />
                <Entry x:Name="NotesEntry" />

                <HorizontalStackLayout Spacing="8" Margin="0,8,0,0">
                    <Button x:Name="AddBtn" Text="+ Add" />
                    <Button x:Name="UpdateBtn" Text="Update" />
                    <Button x:Name="DeleteBtn" Text="Delete" BackgroundColor="#EF4444" TextColor="White" />
                </HorizontalStackLayout>

                <HorizontalStackLayout Spacing="8">
                    <Button x:Name="MultiSelectBtn" Text="☐ Multi-Select OFF" BackgroundColor="#6B7280" TextColor="White" />
                    <Button x:Name="BulkDeleteBtn" Text="🗑️ Delete Selected" BackgroundColor="#DC2626" TextColor="White" IsVisible="False" />
                </HorizontalStackLayout>

                <HorizontalStackLayout Spacing="8">
                    <Button x:Name="SaveBtn" Text="💾 Save" />
                    <Button x:Name="ReloadBtn" Text="🔄 Reload" />
                    <Button x:Name="ExportBtn" Text="📤 Export CSV" />
                </HorizontalStackLayout>

                <ctrls:CsvImportBox x:Name="PlayersImport" Title="Import Players (.csv)" />

                <Label x:Name="StatusLbl" Text="" FontAttributes="Italic" />
            </VerticalStackLayout>
        </ScrollView>
    </Grid>
</ContentPage>