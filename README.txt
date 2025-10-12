# Module "Articles" — clé en main (WPF .NET + EF Core SQLite)

## 1) Prérequis (une seule fois)
Dans le dossier **VorTech.App** :
```powershell
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet tool install --global dotnet-ef
```
Dans votre `App.xaml.cs` (ou bootstrap), enregistrez `AppDbContext` et `ArticleService` via votre conteneur DI habituel (ex. Microsoft.Extensions.DependencyInjection) :
```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using VorTech.App.Data;
using VorTech.App.Services;

var sc = new ServiceCollection();
sc.AddDbContext<AppDbContext>(opt => opt.UseSqlite("Data Source=app.db"));
sc.AddScoped<IArticleService, ArticleService>();
var sp = sc.BuildServiceProvider();
// Gardez sp accessible, ou exposez service via Application.Current.Resources["ArticleService"]
```

> Variante XAML (sans DI) : Ajoutez dans `App.xaml` :
```xml
<Application ... xmlns:svc="clr-namespace:VorTech.App.Services"
               xmlns:data="clr-namespace:VorTech.App.Data">
  <Application.Resources>
    <!-- Pour le constructeur par défaut de ArticlesView, fournissez ArticleService -->
    <svc:ArticleService x:Key="ArticleService">
      <!-- Nécessite un AppDbContext créé en code-behind si pas de DI -->
    </svc:ArticleService>
  </Application.Resources>
</Application>
```

## 2) Copier-coller les fichiers
- `Models/Article.cs`
- `Data/AppDbContext.cs`
- `Services/IArticleService.cs`
- `Services/ArticleService.cs`
- `ViewModels/ArticlesViewModel.cs`
- `Views/ArticlesView.xaml`
- `Views/ArticlesView.xaml.cs`
dans vos dossiers correspondants de **VorTech.App** (respectez les namespaces).

## 3) Générer la base (option A – SQL direct, **immédiat**)
Exécutez le script SQL suivant sur votre base SQLite :
`SQL/reset_articles_sqlite.sql`

## 4) Générer la base (option B – EF Core Migrations)
```powershell
dotnet ef migrations add InitArticles
dotnet ef database update
```

## 5) Afficher l'écran
Ouvrez `ArticlesView` dans votre shell (Window/Page) :
```xml
<views:ArticlesView />
```

**C'est tout.** Vous avez : CRUD complet, recherche, liste + formulaire. 
```