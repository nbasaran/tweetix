using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace WebApplication2.Models
{
    public class CountryModel
    {
        public string name { get; set; }
        public string country { get; set; }
        public int woeid { get; set; }
        public CountryPlaceType placeType { get; set; }
    }

    public class CountryPlaceType
    {
        public string name { get; set; }
        public int code { get; set; }
    }

    public class CountyManager
    {
        public static List<CountryModel> GetCountries()
        {
            var list = new List<CountryModel>();
            using (StreamReader r = new StreamReader(HttpContext.Current.Server.MapPath("~/Content/Data/country.json")))
            {
                string json = r.ReadToEnd();
                list = JsonConvert.DeserializeObject<List<CountryModel>>(json);
                list = list.Where(c => c.placeType.name == "Country" || c.placeType.name == "Supername").ToList();
            }
            return list;
        }
    }

    public class TweetResponse
    {
        public string ID { get; set; }
        public string ScreenName { get; set; }
        public string UserIDResponse { get; set; }
        public string FullText { get; set; }
        public string Lat { get; set; }
        public string Lng { get; set; }
        public int Point { get; set; }
    }
}