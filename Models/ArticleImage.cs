namespace VorTech.App.Models
{
    public class ArticleImage
    {
        public int Id { get; set; }
        public int ArticleId { get; set; }
        public int Ordre { get; set; }           // 1..4
        public string Path { get; set; } = "";   // relatif : Assets/Images/Articles/{code}/img{ordre}.jpg
    }
}
