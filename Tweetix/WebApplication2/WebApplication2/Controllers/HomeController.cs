using LinqToTwitter;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Web.Mvc;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    public class HomeController : Controller
    {
        // GET: Home
        public ActionResult Index()
        {
            ViewBag.tweet = "";

            return View();
        }

        public ActionResult CountryLookup()
        {
            var countries = CountyManager.GetCountries();

            return Json(countries, JsonRequestBehavior.AllowGet);
        }

        public ActionResult TrendsLookup(string woeID)
        {
            var auth = new SingleUserAuthorizer
            {
                CredentialStore = new SingleUserInMemoryCredentialStore
                {
                    ConsumerKey = ConfigurationManager.AppSettings["consumerKey"],
                    ConsumerSecret = ConfigurationManager.AppSettings["consumerSecret"],
                    AccessToken = ConfigurationManager.AppSettings["accessToken"],
                    AccessTokenSecret = ConfigurationManager.AppSettings["accessTokenSecret"]
                }
            };

            var twitterCtx = new TwitterContext(auth);

            var trends = (from trend in twitterCtx.Trends
                          where trend.Type == TrendType.Place &&
                                trend.WoeID == int.Parse(woeID)
                          select trend)
                 .ToList();

            return Json(trends, JsonRequestBehavior.AllowGet);
        }

        public ActionResult UserLookup(string country, string userCategory, string trendString)
        {
            var auth = new SingleUserAuthorizer
            {
                CredentialStore = new SingleUserInMemoryCredentialStore
                {
                    ConsumerKey = ConfigurationManager.AppSettings["consumerKey"],
                    ConsumerSecret = ConfigurationManager.AppSettings["consumerSecret"],
                    AccessToken = ConfigurationManager.AppSettings["accessToken"],
                    AccessTokenSecret = ConfigurationManager.AppSettings["accessTokenSecret"]
                }
            };

            var twitterCtx = new TwitterContext(auth);

            var users = new List<User>();

            if (userCategory == "Politician")
            {
                users.AddRange((from user in twitterCtx.User
                                where user.Type == UserType.Search &&
                                      user.Query == "President," + country && user.Verified == true
                                select user).ToList());

                users.AddRange((from user in twitterCtx.User
                                where user.Type == UserType.Search &&
                                      user.Query == "Minister," + country && user.Verified == true
                                select user).ToList());

                users.AddRange((from user in twitterCtx.User
                                where user.Type == UserType.Search &&
                                      user.Query == "Diplomat," + country && user.Verified == true
                                select user).ToList());
            }
            if (userCategory == "Journalist")
            {
                users.AddRange((from user in twitterCtx.User
                                where user.Type == UserType.Search &&
                                      user.Query == "Journalist," + country && user.Verified == true
                                select user).ToList());

                users.AddRange((from user in twitterCtx.User
                                where user.Type == UserType.Search &&
                                      user.Query == "Gazeteci," + country && user.Verified == true
                                select user).ToList());
            }
            if (userCategory == "Verify People")
            {
                users.AddRange((from user in twitterCtx.User
                                where user.Type == UserType.Search &&
                                      user.Query == country && user.Verified == true
                                select user).ToList());
            }
            if (userCategory == "Other")
            {
                users.AddRange((from user in twitterCtx.User
                                where user.Type == UserType.Search &&
                                      user.Query == country && user.Verified == false
                                select user).ToList());
            }

            Session["UserList"] = users;
            Session["UserCategory"] = userCategory;
            Session["Trend"] = trendString;


            var tweetList = new List<Status>();

            foreach (var usr in users)
            {
                tweetList.AddRange((from tweet in twitterCtx.Status where tweet.Type == StatusType.User && tweet.ScreenName == usr.ScreenNameResponse select tweet).ToList());
            }


            tweetList = tweetList.Where(t => t.Text.Contains(trendString)).ToList();

            Session["TweetList"] = tweetList;

            return Json(new { users = users, tweetList = tweetList }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult TweetsLookup(string trendId)
        {
            var resultList = new List<TweetResponse>();

            var auth = new SingleUserAuthorizer
            {
                CredentialStore = new SingleUserInMemoryCredentialStore
                {
                    ConsumerKey = ConfigurationManager.AppSettings["consumerKey"],
                    ConsumerSecret = ConfigurationManager.AppSettings["consumerSecret"],
                    AccessToken = ConfigurationManager.AppSettings["accessToken"],
                    AccessTokenSecret = ConfigurationManager.AppSettings["accessTokenSecret"]
                }
            };

            var twitterCtx = new TwitterContext(auth);


            string searchTerm = trendId;
            //searchTerm = "кот (";

            const int MaxSearchEntriesToReturn = 10;
            const int MaxTotalResults = 100;

            // oldest id you already have for this search term
            ulong sinceID = 1;

            // used after the first query to track current session
            ulong maxID;

            var combinedSearchResults = new List<Status>();

            List<Status> searchResponse = (from search in twitterCtx.Search
                                           where search.Type == SearchType.Search &&
                                                 search.Query == searchTerm &&
                                                 search.Count == MaxSearchEntriesToReturn &&
                                                 search.SinceID == sinceID &&
                                                 search.TweetMode == TweetMode.Extended
                                           select search.Statuses)
                .SingleOrDefault();

            if (searchResponse != null)
            {
                combinedSearchResults.AddRange(searchResponse);
                ulong previousMaxID = ulong.MaxValue;
                do
                {
                    // one less than the newest id you've just queried
                    maxID = searchResponse.Min(status => status.StatusID) - 1;

                    Debug.Assert(maxID < previousMaxID);
                    previousMaxID = maxID;

                    searchResponse = (from search in twitterCtx.Search
                                      where search.Type == SearchType.Search &&
                                            search.Query == searchTerm &&
                                            search.Count == MaxSearchEntriesToReturn &&
                                            search.MaxID == maxID &&
                                            search.SinceID == sinceID &&
                                            search.TweetMode == TweetMode.Extended
                                      select search.Statuses)
                        .SingleOrDefault();

                    combinedSearchResults.AddRange(searchResponse);
                } while (searchResponse.Any() && combinedSearchResults.Count < MaxTotalResults);

                foreach (var item in combinedSearchResults.Where(t => t.User.GeoEnabled == true && t.User.Location != ""))
                {
                    var model = new TweetResponse();
                    model.ID = item.StatusID.ToString();
                    model.FullText = item.FullText;
                    model.ScreenName = item.User.Name;
                    model.UserIDResponse = item.User.UserIDResponse;
                    model.Lat = item.User.Location.ToString();
                    model.Lng = item.Coordinates.Longitude.ToString();

                    resultList.Add(model);
                }
            }
            else
            {
                Console.WriteLine("No entries found.");
            }

            Session["TrendQuery"] = trendId;

            Session["TweetList"] = resultList;

            return Json(resultList, JsonRequestBehavior.AllowGet);
        }

        public ActionResult TweetsForUserLookup(string userName)
        {
            var auth = new SingleUserAuthorizer
            {
                CredentialStore = new SingleUserInMemoryCredentialStore
                {
                    ConsumerKey = ConfigurationManager.AppSettings["consumerKey"],
                    ConsumerSecret = ConfigurationManager.AppSettings["consumerSecret"],
                    AccessToken = ConfigurationManager.AppSettings["accessToken"],
                    AccessTokenSecret = ConfigurationManager.AppSettings["accessTokenSecret"]
                }
            };

            var twitterCtx = new TwitterContext(auth);

            var users = (List<User>)Session["Users"];

            var user = users.FirstOrDefault(u => u.Name == userName);

            var tweetList = new List<Status>();

            tweetList.AddRange((from tweet in twitterCtx.Status where tweet.Type == StatusType.User && tweet.ScreenName == user.ScreenNameResponse select tweet).ToList());

            tweetList = tweetList.Where(t => t.Text.Contains(Session["Trend"].ToString())).ToList();

            var resultList = new List<TweetResponse>();

            foreach (var item in tweetList)
            {
                var model = new TweetResponse();
                model.ID = item.StatusID.ToString();
                model.FullText = item.FullText;
                model.ScreenName = item.User.Name;
                model.UserIDResponse = item.User.UserIDResponse;
                model.Lat = item.User.Location.ToString();
                model.Lng = item.Coordinates.Longitude.ToString();

                resultList.Add(model);
            }

            Session["TweetList"] = resultList;

            return Json(resultList, JsonRequestBehavior.AllowGet);
        }

        public ActionResult RateLookup()
        {
            var response = new List<TweetResponse>();

            using (var tContext = new TweetixContext())
            {
                response = tContext.TweetResponses.ToList();
            }

            return Json(response, JsonRequestBehavior.AllowGet);
        }

        public ActionResult VoteTweet(string tId, string voteType)
        {

            var tweets = (List<TweetResponse>)Session["TweetList"];

            using (var tContext = new TweetixContext())
            {
                var model = tContext.TweetResponses.FirstOrDefault(b => b.ID == tId);
                if (model != null)
                {
                    if (voteType == "u")
                    {
                        model.Point++;
                    }
                    else
                    {
                        model.Point--;
                    }

                    tContext.SaveChanges();
                }
                else
                {
                    var selectedItem = tweets.FirstOrDefault(t => t.ID == tId);
                    if (selectedItem != null)
                    {
                        if (voteType == "u")
                        {
                            selectedItem.Point++;
                        }
                        else
                        {
                            selectedItem.Point--;
                        }
                        tContext.TweetResponses.Add(selectedItem);
                        tContext.SaveChanges();
                    }

                }

            }

            return Json(true, JsonRequestBehavior.AllowGet);
        }
    }

    public class TweetixContext : System.Data.Entity.DbContext
    {
        public TweetixContext() : base("name=TweetixDb")
        {
            Database.SetInitializer<TweetixContext>(new CreateDatabaseIfNotExists<TweetixContext>());
        }

        public DbSet<TweetResponse> TweetResponses { get; set; }

    }
}