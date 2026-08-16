using System;
using System.Collections.Generic;

namespace AkmazBackend.Models
{
    public class Product
    {
        public int Id { get; set; }

        // Match your DB column names
        public string Product_Name { get; set; } = string.Empty;
        public string Quantity { get; set; } = string.Empty;
        public decimal Price { get; set; }

        public string Category { get; set; } = "fish"; // default
        public string Other_Details { get; set; } = "[]"; // store JSON as string
        public DateTime Created_At { get; set; } = DateTime.Now;
    }
}
