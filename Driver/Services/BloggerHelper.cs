using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Blogger.v3;
using Google.Apis.Blogger.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace GooService
{
	static class BloggerHelper
	{
		public static UserCredential createCredential(ClientSecrets cs, FileDataStore fds)
		{
			UserCredential uc = GoogleWebAuthorizationBroker.AuthorizeAsync(
				cs,
				new[] { BloggerService.Scope.Blogger },
				"user",
				CancellationToken.None,
				fds
				).Result;
			return uc;
		}

		public static BloggerService createService(UserCredential uc, string appName)
		{
			BloggerService bs = new BloggerService(new BaseClientService.Initializer
				{
					HttpClientInitializer = uc,
					ApplicationName = appName
				});
			return bs;
		}

		public static Post _getPost(this BloggerService bs, string blogId, string postId)
		{
			PostsResource.GetRequest req = bs.Posts.Get(blogId, postId);
			req.View = PostsResource.GetRequest.ViewEnum.ADMIN;

			Post p = req.Execute();
			return p;
		}

		public static List<Post> _getPostList(this BloggerService bs, string blogId, PostsResource.ListRequest.StatusEnum status)
		{
			PostsResource.ListRequest req = bs.Posts.List(blogId);
			req.View = PostsResource.ListRequest.ViewEnum.ADMIN;
			req.FetchBodies = false;
			req.FetchImages = false;
			req.Status = status;

			List<Post> listOfPost = new List<Post>();
			string firstToken = "";

			while (true)
			{
				PostList posts = req.Execute();
				req.PageToken = posts.NextPageToken;
				if (firstToken == "")
				{
					firstToken = posts.NextPageToken;
				}
				else if (firstToken != "" && posts.NextPageToken == firstToken)
				{
					break;
				}
				if (posts.Items != null)
				{
					posts.Items.ToList().ForEach(item => listOfPost.Add(item));
				}
			}

			return listOfPost;

		}

		public static List<Pageviews.CountsData> _getPageViews(this BloggerService bs, string blogId, PageViewsResource.GetRequest.RangeEnum range)
		{
			if (bs == null || _getBlogUserInfo(bs, blogId).HasAdminAccess != true)
			{
				return null;
			}

			PageViewsResource.GetRequest req = bs.PageViews.Get(blogId);
			req.Range = range;

			Pageviews pv = req.Execute();
			return pv.Counts.ToList();
		}

		public static BlogPerUserInfo _getBlogUserInfo(this BloggerService bs, string blogId)
		{
			if (bs == null)
			{
				return null;
			}

			BlogUserInfosResource.GetRequest req = bs.BlogUserInfos.Get("self", blogId);
			BlogUserInfo bui = req.Execute();

			return bui.BlogUserInfoValue;
		}

		public static List<Blog> _listAllBlogs(this BloggerService bs)
		{
			if (bs == null)
			{
				return null;
			}

			BlogsResource.ListByUserRequest req = bs.Blogs.ListByUser("self");
			BlogList bl = req.Execute();

			return bl.Items.ToList();
		}

        public static async Task<IEnumerable<Blog>> _getBlogsAsync(this BloggerService service)
        {
            var list = await service.Blogs.ListByUser("self").ExecuteAsync();
            return from blog in list.Items
                   select new Blog
                   {
                       Id = blog.Id,
                       Name = blog.Name
                   };
        }

        public static string _addPost(this BloggerService service, string blogId, Post post)
        {
            try
            {
                var insertReq = service.Posts.Insert(post, blogId).Execute();
                return insertReq.Url;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public static async Task<string> _addPostToBlogAsync(this BloggerService service, string blogId, Post post)
        {
            try
            {
                var insertReq = await service.Posts.Insert(post, blogId).ExecuteAsync();
                return insertReq.Url;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public static async Task<string> _updatePostToBlogAsync(this BloggerService service, string blogId, Post post)
        {
            var updateReq = await service.Posts.Update(post, blogId, post.Id).ExecuteAsync();
            return updateReq.Url;
        }
        public static async Task<IEnumerable<Post>> _getPostByLabel(this BloggerService service, string blogId, string label)
        {
            try
            {
                var req = service.Posts.List(blogId);
                var reqRs = await req.ExecuteAsync();
                List<Post> list = new List<Post>();
                list = reqRs.Items.Where(p => p.Labels != null && p.Labels.FirstOrDefault().Equals(label)).ToList();
                while (reqRs.NextPageToken != null)
                {
                    req.PageToken = reqRs.NextPageToken;
                    reqRs = await req.ExecuteAsync();
                    var rs = reqRs.Items.Where(p => p.Labels != null && p.Labels.FirstOrDefault().Equals(label)).ToList();
                    if (rs != null)
                    {
                        list.AddRange(rs);
                    }
                }
                return list;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public static async Task<IEnumerable<Post>> _getPostNoneLabel(this BloggerService service, string blogId)
        {
            try
            {
                var req = service.Posts.List(blogId);
                var reqRs = await req.ExecuteAsync();
                List<Post> list = new List<Post>();
                list = reqRs.Items.Where(p => p.Labels == null).ToList();
                while (reqRs.NextPageToken != null)
                {
                    req.PageToken = reqRs.NextPageToken;
                    reqRs = await req.ExecuteAsync();
                    var rs = reqRs.Items.Where(p => p.Labels == null).ToList();
                    if (rs != null)
                    {
                        list.AddRange(rs);
                    }
                }
                return list;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public static async Task<IEnumerable<Post>> _getPostByPublishedTime(this BloggerService service, string blogId, DateTime from, DateTime to)
        {
            try
            {
                DateTime.Compare(from, to);
                var req = service.Posts.List(blogId);
                var reqRs = await req.ExecuteAsync();
                List<Post> list = new List<Post>();
                
                list = reqRs.Items.Where(p => DateTime.Compare(DateTime.Parse(p.Published), from) > 0 && DateTime.Compare(DateTime.Parse(p.Published), to) < 0).ToList();
                while (reqRs.NextPageToken != null)
                {
                    req.PageToken = reqRs.NextPageToken;
                    reqRs = await req.ExecuteAsync();
                    var rs = reqRs.Items.Where(p => DateTime.Compare(DateTime.Parse(p.Published), from) > 0 && DateTime.Compare(DateTime.Parse(p.Published), to) < 0).ToList();
                    if (rs != null)
                    {
                        list.AddRange(rs);
                    }
                }
                return list;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
