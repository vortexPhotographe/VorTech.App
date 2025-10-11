namespace VorTech.App.Models
{
    public class ArticleImage
    {
        public int Id { get; set; }
        public int ArticleId { get; set; }
        public string RelPath { get; set; } = "";    // ex: images/articles/{ArticleId}/photo1.jpg (relatif)
        public string? Caption { get; set; }
        public int Order { get; set; } = 0;
    }
}
