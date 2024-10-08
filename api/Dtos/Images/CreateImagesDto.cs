using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace api.Dtos.Images
{
  public class CreateImagesDto
  {
    [Required]
    [MinLength(1, ErrorMessage = "Title is required")]
    public string Title { get; set; } = string.Empty;
    [Required]
    [MaxLength(50, ErrorMessage = "Description cannot be over 50 characters")]
    public string Description { get; set; } = string.Empty;
    [Required]
    [MaxLength(100, ErrorMessage = "Image is required")]
    public string ImageURL { get; set; } = string.Empty;
    public List<string> ImageTags { get; set; } = new List<string>();
  }
}