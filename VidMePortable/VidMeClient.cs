﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using VidMePortable.Extensions;
using VidMePortable.Model;
using VidMePortable.Model.Responses;

namespace VidMePortable
{
    public class VidMeClient : IVidMeClient
    {
        private const string BaseUrl = "https://api.vid.me/";
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Gets the authentication information.
        /// </summary>
        /// <value>
        /// The authentication information.
        /// </value>
        public Auth AuthenticationInfo { get; private set; }
        /// <summary>
        /// Gets the device identifier.
        /// </summary>
        /// <value>
        /// The device identifier.
        /// </value>
        public string DeviceId { get; private set; }
        /// <summary>
        /// Gets the platform.
        /// </summary>
        /// <value>
        /// The platform.
        /// </value>
        public string Platform { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="VidMeClient"/> class.
        /// </summary>
        public VidMeClient()
            : this(string.Empty, string.Empty) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="VidMeClient"/> class.
        /// </summary>
        /// <param name="deviceName">Name of the device.</param>
        /// <param name="platform">The platform.</param>
        public VidMeClient(string deviceName, string platform)
        {
            DeviceId = deviceName;
            Platform = platform;
            _httpClient = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip });
        }

        /// <summary>
        /// Sets the device name and platform.
        /// </summary>
        /// <param name="deviceId">The device identifier.</param>
        /// <param name="platform">The platform.</param>
        public void SetDeviceNameAndPlatform(string deviceId, string platform)
        {
            DeviceId = deviceId;
            Platform = platform;
        }

        #region Auth Methods

        /// <summary>
        /// Authenticates the.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// username;username cannot be null or empty
        /// or
        /// password;password cannot be null or empty
        /// </exception>
        [Obsolete("This method will work, but the oauth way is preferred")]
        public async Task<AuthResponse> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentNullException("username", "username cannot be null or empty");
            }

            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentNullException("password", "password cannot be null or empty");
            }

            var postData = new Dictionary<string, string>
            {
                {"username", username},
                {"password", password}
            };

            var response = await Post<AuthResponse>(postData, "auth/create", cancellationToken);
            if (response != null)
            {
                SetAuthentication(response.Auth);
            }

            return response;
        }

        /// <summary>
        /// Checks the authentication token.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="VidMeException">No AuthenticationInfo set</exception>
        public async Task<AuthResponse> CheckAuthTokenAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (AuthenticationInfo == null)
            {
                throw new VidMeException(HttpStatusCode.Unauthorized, "No AuthenticationInfo set");
            }

            var postData = CreatePostData();
            var response = await Post<AuthResponse>(postData, "auth/check", cancellationToken);

            if (response != null)
            {
                SetAuthentication(response.Auth);
            }

            return response;
        }

        /// <summary>
        /// Deletes the authentication token.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="VidMeException">No AuthenticationInfo set</exception>
        public async Task<bool> DeleteAuthTokenAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (AuthenticationInfo == null)
            {
                throw new VidMeException(HttpStatusCode.Unauthorized, "No AuthenticationInfo set");
            }

            var postData = CreatePostData();
            var response = await Post<Response>(postData, "auth/delete", cancellationToken);

            return response != null && response.Status;
        }

        /// <summary>
        /// Gets the authentication URL.
        /// </summary>
        /// <param name="clientId">The client identifier.</param>
        /// <param name="redirectUrl">The redirect URL.</param>
        /// <param name="scopes">The scopes.</param>
        /// <param name="type">The type.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// clientId;Client ID cannot be null or empty
        /// or
        /// scopes;Scopes must be provided
        /// </exception>
        public string GetAuthUrl(string clientId, string redirectUrl, List<Scope> scopes, AuthType type = AuthType.Code)
        {
            if (string.IsNullOrEmpty(clientId))
            {
                throw new ArgumentNullException("clientId", "Client ID cannot be null or empty");
            }

            if (scopes.IsNullOrEmpty())
            {
                throw new ArgumentNullException("scopes", "Scopes must be provided");
            }

            var scopesString = string.Join(" ", scopes.Select(x => x.GetDescription()));

            return string.Format("https://vid.me/oauth/authorize?client_id={0}&redirect_uri={1}&scope={2}&response_type={3}", clientId, redirectUrl, scopesString, type.ToString().ToLower());
        }

        /// <summary>
        /// Exchanges the code for token.
        /// </summary>
        /// <param name="code">The code.</param>
        /// <param name="clientId">The client identifier.</param>
        /// <param name="clientSecret">The client secret.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// code;code cannot be null or empty
        /// or
        /// clientId;Client ID cannot be null or empty
        /// or
        /// clientSecret;Client Secret cannot be null or empty
        /// </exception>
        public async Task<AuthResponse> ExchangeCodeForTokenAsync(string code, string clientId, string clientSecret, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(code))
            {
                throw new ArgumentNullException("code", "code cannot be null or empty");
            }

            if (string.IsNullOrEmpty(clientId))
            {
                throw new ArgumentNullException("clientId", "Client ID cannot be null or empty");
            }

            if (string.IsNullOrEmpty(clientSecret))
            {
                throw new ArgumentNullException("clientSecret", "Client Secret cannot be null or empty");
            }

            var postData = new Dictionary<string, string>
            {
                {"grant_type", "client_credentials"},
                {"client_id", clientId},
                {"client_secret", clientSecret},
                {"code", code}
            };

            var response = await Post<AuthResponse>(postData, "oauth/token", cancellationToken);

            if (response != null)
            {
                SetAuthentication(response.Auth);
            }

            return response;
        }

        /// <summary>
        /// Sets the authentication.
        /// </summary>
        /// <param name="authenticationInfo">The authentication information.</param>
        public void SetAuthentication(Auth authenticationInfo)
        {
            AuthenticationInfo = authenticationInfo;
        }

        #endregion

        #region Channel Methods

        /// <summary>
        /// Gets the channel.
        /// </summary>
        /// <param name="channelId">The channel identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">channelId;Channel ID cannot be null or empty</exception>
        public async Task<Channel> GetChannelAsync(string channelId, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(channelId))
            {
                throw new ArgumentNullException("channelId", "Channel ID cannot be null or empty");
            }

            var method = string.Format("channel/{0}", channelId);

            var response = await Get<ChannelResponse>(method, cancellationToken: cancellationToken);

            return response != null ? response.Channel : null;
        }

        /// <summary>
        /// Follows the channel.
        /// </summary>
        /// <param name="channelId">The channel identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">channelId;Channel ID cannot be null or empty</exception>
        public async Task<bool> FollowChannelAsync(string channelId, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(channelId))
            {
                throw new ArgumentNullException("channelId", "Channel ID cannot be null or empty");
            }

            var postData = CreatePostData();
            var method = string.Format("channel/{0}/follow", channelId);

            var response = await Post<Response>(postData, method, cancellationToken);

            return response != null && response.Status;
        }

        /// <summary>
        /// Gets the channels hot videos.
        /// </summary>
        /// <param name="channelId">The channel identifier.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="limit">The limit.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">channelId;Channel ID cannot be null or empty</exception>
        public async Task<VideosResponse> GetChannelsHotVideosAsync(string channelId, int? offset = null, int? limit = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(channelId))
            {
                throw new ArgumentNullException("channelId", "Channel ID cannot be null or empty");
            }

            var method = string.Format("channel/{0}/hot", channelId);
            var options = new Dictionary<string, string>();
            options.AddIfNotNull("offset", offset);
            options.AddIfNotNull("limit", limit);

            var response = await Get<VideosResponse>(method, options.ToQueryString(), cancellationToken);
            return response;
        }

        /// <summary>
        /// Gets the channels new videos.
        /// </summary>
        /// <param name="channelId">The channel identifier.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="limit">The limit.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">channelId;Channel ID cannot be null or empty</exception>
        public async Task<VideosResponse> GetChannelsNewVideosAsync(string channelId, int? offset = null, int? limit = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(channelId))
            {
                throw new ArgumentNullException("channelId", "Channel ID cannot be null or empty");
            }

            var method = string.Format("channel/{0}/new", channelId);
            var options = new Dictionary<string, string>();
            options.AddIfNotNull("offset", offset);
            options.AddIfNotNull("limit", limit);

            var response = await Get<VideosResponse>(method, options.ToQueryString(), cancellationToken);
            return response;
        }

        /// <summary>
        /// Gets the channel URL.
        /// </summary>
        /// <param name="channelId">The channel identifier.</param>
        /// <returns></returns>
        public string GetChannelUrl(string channelId)
        {
            return CreateUrl(string.Format("channel/{0}/url", channelId));
        }

        /// <summary>
        /// Uns the follow channel.
        /// </summary>
        /// <param name="channelId">The channel identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">channelId;Channel ID cannot be null or empty</exception>
        public async Task<bool> UnFollowChannelAsync(string channelId, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(channelId))
            {
                throw new ArgumentNullException("channelId", "Channel ID cannot be null or empty");
            }

            var postData = CreatePostData();
            var method = string.Format("channel/{0}/unfollow", channelId);

            var response = await Post<Response>(postData, method, cancellationToken);

            return response != null && response.Status;
        }

        /// <summary>
        /// Lists the channels.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public async Task<List<Channel>> ListChannelsAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            var response = await Get<ChannelsResponse>("channels", cancellationToken: cancellationToken);
            if (response != null)
            {
                return response.Channels ?? new List<Channel>();
            }

            return new List<Channel>();
        }

        /// <summary>
        /// Lists the suggested channels.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="number">The number.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public async Task<List<Channel>> ListSuggestedChannelsAsync(string text = null, int? number = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var options = new Dictionary<string, string>();
            options.AddIfNotNull("text", text);
            options.AddIfNotNull("number", number);

            var response = await Get<ChannelsResponse>("channels/suggest", options.ToQueryString(), cancellationToken);
            if (response != null)
            {
                return response.Channels ?? new List<Channel>();
            }

            return new List<Channel>();
        }

        #endregion

        #region Comment Methods

        /// <summary>
        /// Creates the comment.
        /// </summary>
        /// <param name="videoId">The video identifier.</param>
        /// <param name="commentText">The comment text.</param>
        /// <param name="timeOfComment">The time of comment.</param>
        /// <param name="inReplyToCommentId">The in reply to comment identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// videoId;Video ID cannot be null or empty
        /// or
        /// commentText;Empty comments are not allowed
        /// </exception>
        public async Task<Comment> CreateCommentAsync(string videoId, string commentText, TimeSpan timeOfComment, string inReplyToCommentId = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(videoId))
            {
                throw new ArgumentNullException("videoId", "Video ID cannot be null or empty");
            }

            if (string.IsNullOrEmpty(commentText))
            {
                throw new ArgumentNullException("commentText", "Empty comments are not allowed");
            }

            if (timeOfComment == TimeSpan.MinValue)
            {
                timeOfComment = TimeSpan.FromSeconds(0);
            }

            var postData = CreatePostData();
            postData.AddIfNotNull("video", videoId);
            postData.AddIfNotNull("comment", inReplyToCommentId);
            postData.AddIfNotNull("body", commentText);
            postData.AddIfNotNull("at", timeOfComment.TotalSeconds);

            var response = await Post<CommentResponse>(postData, "comment/create", cancellationToken);

            return response != null ? response.Comment : null;
        }

        /// <summary>
        /// Deletes the comment.
        /// </summary>
        /// <param name="commentId">The comment identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">commentId;Comment ID cannot be null or empty</exception>
        public async Task<bool> DeleteCommentAsync(string commentId, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(commentId))
            {
                throw new ArgumentNullException("commentId", "Comment ID cannot be null or empty");
            }

            var postData = CreatePostData();
            var method = string.Format("comment/{0}/delete", commentId);

            var response = await Post<Response>(postData, method, cancellationToken);
            return response != null && response.Status;
        }

        /// <summary>
        /// Gets the comment.
        /// </summary>
        /// <param name="commentId">The comment identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">commentId;Comment ID cannot be null or empty</exception>
        public async Task<Comment> GetCommentAsync(string commentId, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(commentId))
            {
                throw new ArgumentNullException("commentId", "Comment ID cannot be null or empty");
            }

            var method = string.Format("comment/{0}", commentId);
            var response = await Get<CommentResponse>(method, cancellationToken: cancellationToken);

            return response != null ? response.Comment : null;
        }

        /// <summary>
        /// Gets the comments.
        /// </summary>
        /// <param name="videoId">The video identifier.</param>
        /// <param name="sortDirection">The sort direction.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">videoId;Video ID cannot be null or empty</exception>
        public async Task<List<Comment>> GetCommentsAsync(string videoId, SortDirection? sortDirection, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(videoId))
            {
                throw new ArgumentNullException("videoId", "Video ID cannot be null or empty");
            }

            var options = new Dictionary<string, string>();
            options.AddIfNotNull("video", videoId);
            options.AddIfNotNull("direction", sortDirection.GetDescription());
            
            var response = await Get<CommentsResponse>("comments/list", options.ToQueryString(), cancellationToken);

            if (response != null)
            {
                return response.Comments ?? new List<Comment>();
            }

            return new List<Comment>();
        }

        /// <summary>
        /// Gets the comment URL.
        /// </summary>
        /// <param name="commentId">The comment identifier.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">commentId;Comment ID cannot be null or empty</exception>
        public string GetCommentUrl(string commentId)
        {
            if (string.IsNullOrEmpty(commentId))
            {
                throw new ArgumentNullException("commentId", "Comment ID cannot be null or empty");
            }

            return CreateUrl(string.Format("comment/{0}/url", commentId));
        }

        /// <summary>
        /// Votes the comment.
        /// </summary>
        /// <param name="commentId">The comment identifier.</param>
        /// <param name="vote">The vote.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">commentId;Comment ID cannot be null or empty</exception>
        public async Task<Comment> VoteCommentAsync(string commentId, Vote vote, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(commentId))
            {
                throw new ArgumentNullException("commentId", "Comment ID cannot be null or empty");
            }

            var postData = CreatePostData();
            postData.AddIfNotNull("value", vote.GetDescription());

            var method = string.Format("comment/{0}/vote", commentId);

            var response = await Post<CommentResponse>(postData, method, cancellationToken);

            return response != null ? response.Comment : null;
        }

        #endregion

        #region GeoFences Methods

        /// <summary>
        /// Gets the geo fences.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public async Task<List<Geofence>> GetGeoFencesAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            var response = await Get<GeoFencesResponse>("geofences", cancellationToken: cancellationToken);
            if (response != null)
            {
                return response.Geofences ?? new List<Geofence>();
            }

            return new List<Geofence>();
        }

        /// <summary>
        /// Suggests the geo fences.
        /// </summary>
        /// <param name="searchText">The search text.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public async Task<List<Geofence>> SuggestGeoFencesAsync(string searchText = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var options = new Dictionary<string, string>();
            options.AddIfNotNull("text", searchText);

            var response = await Get<GeoFencesResponse>("geofences/suggest", options.ToQueryString(), cancellationToken);
            if (response != null)
            {
                return response.Geofences ?? new List<Geofence>();
            }

            return new List<Geofence>();
        }

        #endregion

        #region Grab Methods

        /// <summary>
        /// Grabs the external video.
        /// </summary>
        /// <param name="externalUrl">The external URL.</param>
        /// <param name="title">The title.</param>
        /// <param name="description">The description.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">externalUrl;External URL cannot be null or empty</exception>
        public async Task<Video> GrabExternalVideoAsync(string externalUrl, string title = null, string description = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(externalUrl))
            {
                throw new ArgumentNullException("externalUrl", "External URL cannot be null or empty");
            }

            var postData = CreatePostData(false);
            postData.AddIfNotNull("url", externalUrl);
            postData.AddIfNotNull("title", title);
            postData.AddIfNotNull("description", description);

            var response = await Post<VideoResponse>(postData, "grab", cancellationToken);
            return response != null ? response.Video : null;
        }

        /// <summary>
        /// Grabs the external video information.
        /// </summary>
        /// <param name="externalUrl">The external URL.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">externalUrl;External URL cannot be null or empty</exception>
        public async Task<VideoInfoResponse> GrabExternalVideoInfoAsync(string externalUrl, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(externalUrl))
            {
                throw new ArgumentNullException("externalUrl", "External URL cannot be null or empty");
            }

            var options = new Dictionary<string, string>();
            options.AddIfNotNull("url", externalUrl);

            var response = await Get<VideoInfoResponse>("grab/preview", options.ToQueryString(), cancellationToken);
            return response;
        }

        #endregion

        #region Notifications Methods

        /// <summary>
        /// Gets the notifications.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public async Task<List<Notification>> GetNotificationsAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            var postData = CreatePostData();

            var response = await Post<NotificationsResponse>(postData, "notifications", cancellationToken);
            if (response != null)
            {
                return response.Notifications ?? new List<Notification>();
            }

            return new List<Notification>();
        }

        /// <summary>
        /// Marks the notifications as read.
        /// </summary>
        /// <param name="notificationIds">The notification ids.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">notificationIds;You must provide a list of notification IDs</exception>
        public async Task<bool> MarkNotificationsAsReadAsync(List<string> notificationIds, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (notificationIds.IsNullOrEmpty())
            {
                throw new ArgumentNullException("notificationIds", "You must provide a list of notification IDs");
            }

            var postData = CreatePostData();
            postData.AddIfNotNull("notifications", notificationIds);

            var response = await Post<Response>(postData, "notifications/mark-read", cancellationToken);
            return response != null && response.Status;
        }

        /// <summary>
        /// Marks all notifications as read.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public async Task<bool> MarkAllNotificationsAsReadAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            var postData = CreatePostData();
            postData.Add("notifications", "all");

            var response = await Post<Response>(postData, "notifications/mark-read", cancellationToken);
            return response != null && response.Status;
        }

        #endregion

        #region Tags Methods

        /// <summary>
        /// Suggesteds the tags.
        /// </summary>
        /// <param name="searchText">The search text.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public async Task<List<Tag>> SuggestedTagsAsync(string searchText = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var postData = CreatePostData(false);
            postData.AddIfNotNull("text", searchText);

            var response = await Post<TagsResponse>(postData, "tags/suggest", cancellationToken);
            if (response != null)
            {
                return response.Tags ?? new List<Tag>();
            }

            return new List<Tag>();
        }

        #endregion

        #region User Methods

        /// <summary>
        /// Gets the user avatar.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">userId;User ID cannot be null or empty</exception>
        public string GetUserAvatar(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId", "User ID cannot be null or empty");
            }

            var method = string.Format("user/{0}/avatar", userId);

            return CreateUrl(method);
        }

        /// <summary>
        /// Creates the user.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <param name="email">The email.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// username;username cannot be null or empty
        /// or
        /// password;password cannot be null or empty
        /// </exception>
        public async Task<AuthResponse> CreateUserAsync(string username, string password, string email = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentNullException("username", "username cannot be null or empty");
            }

            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentNullException("password", "password cannot be null or empty");
            }

            var postData = new Dictionary<string, string>
            {
                {"username", username},
                {"password", password}
            };

            postData.AddIfNotNull("email", email);

            var response = await Post<AuthResponse>(postData, "user/create", cancellationToken);
            if (response != null)
            {
                SetAuthentication(response.Auth);
            }

            return response;
        }

        /// <summary>
        /// Gets the user.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">userId;User Id cannot be null or empty</exception>
        public async Task<User> GetUserAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId", "User Id cannot be null or empty");
            }

            var method = string.Format("user/{0}", userId);

            var response = await Post<UserResponse>(new Dictionary<string, string>(), method, cancellationToken);
            return response != null ? response.User : null;
        }

        /// <summary>
        /// Edits the user.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="username">The username.</param>
        /// <param name="currentPassword">The current password.</param>
        /// <param name="newPassword">The new password.</param>
        /// <param name="email">The email.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">userId;User Id cannot be null or empty</exception>
        public async Task<AuthResponse> EditUserAsync(string userId, string username = null, string currentPassword = null, string newPassword = null, string email = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId", "User Id cannot be null or empty");
            }
            
            var postData = CreatePostData();

            postData.AddIfNotNull("username", username);
            postData.AddIfNotNull("email", email);
            postData.AddIfNotNull("password", newPassword);
            postData.AddIfNotNull("passwordCurrent", currentPassword);
            postData.AddIfNotNull("bio", "");

            postData.AddIfNotNull("email", email);

            var response = await Post<AuthResponse>(postData, "user/edit", cancellationToken);
            if (response != null)
            {
                SetAuthentication(response.Auth);
            }

            return response;
        }

        /// <summary>
        /// Follows the user.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">userId;User ID cannot be null or empty</exception>
        public async Task<bool> FollowUserAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId", "User ID cannot be null or empty");
            }

            var postData = CreatePostData();

            var method = string.Format("user/{0}/follow", userId);

            var response = await Post<Response>(postData, method, cancellationToken);

            return response != null && response.Status;
        }

        /// <summary>
        /// Gets the users followed channels.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">userId;User ID cannot be null or empty</exception>
        public async Task<List<Channel>> GetUsersFollowedChannelsAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId", "User ID cannot be null or empty");
            }

            var postData = CreatePostData();
            var method = string.Format("user/{0}/follows-channels", userId);

            var response = await Post<ChannelsResponse>(postData, method, cancellationToken);

            if (response != null)
            {
                return response.Channels ?? new List<Channel>();
            }

            return new List<Channel>();
        }

        /// <summary>
        /// Removes the avatar.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">userId;User ID cannot be null or empty</exception>
        public async Task<bool> RemoveAvatarAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId", "User ID cannot be null or empty");
            }

            var postData = CreatePostData();
            var method = string.Format("user/{0}/avatar/remove", userId);

            var response = await Post<AuthResponse>(postData, method, cancellationToken);

            return response != null && response.Status;
        }

        /// <summary>
        /// Unfollows the user.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">userId;User ID cannot be null or empty</exception>
        public async Task<bool> UnfollowUserAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId", "User ID cannot be null or empty");
            }

            var postData = CreatePostData();
            var method = string.Format("user/{0}/unfollow", userId);

            var response = await Post<Response>(postData, method, cancellationToken);

            return response != null && response.Status;
        }

        /// <summary>
        /// Updates the avatar.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="imageStream">The image stream.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">userId;User ID cannot be null or empty</exception>
        public async Task<User> UpdateAvatarAsync(string userId, Stream imageStream, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId", "User ID cannot be null or empty");
            }

            using (var memoryStream = new MemoryStream())
            {
                await imageStream.CopyToAsync(memoryStream);
                return await UpdateAvatarAsync(userId, memoryStream.ToArray(), cancellationToken);
            }
        }

        /// <summary>
        /// Updates the avatar.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="imageStream">The image stream.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">userId;User ID cannot be null or empty</exception>
        public async Task<User> UpdateAvatarAsync(string userId, byte[] imageStream, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId", "User ID cannot be null or empty");
            }

            var postData = CreatePostData();
            var imageData = Convert.ToBase64String(imageStream);
            postData.Add("filedata", imageData);

            var method = string.Format("user/{0}/avatar/update", userId);

            var response = await Post<UserResponse>(postData, method, cancellationToken);

            return response != null ? response.User : null;
        }

        /// <summary>
        /// Suggesteds the users.
        /// </summary>
        /// <param name="searchText">The search text.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public async Task<List<UserTag>> SuggestedUsersAsync(string searchText = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var postData = CreatePostData(false);
            postData.AddIfNotNull("text", searchText);

            var response = await Post<UserTagsResponse>(postData, "users/suggest", cancellationToken);
            if (response != null)
            {
                return response.UserTags ?? new List<UserTag>();
            }

            return new List<UserTag>();
        }

        /// <summary>
        /// Gets the user videos.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="limit">The limit.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">userId;User ID cannot be null or empty</exception>
        public async Task<VideosResponse> GetUserVideosAsync(string userId, int? offset = null, int? limit = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId", "User ID cannot be null or empty");
            }

            var options = CreatePostData(false);
            options.AddIfNotNull("user", userId);
            options.AddIfNotNull("offset", offset);
            options.AddIfNotNull("limit", limit);

            var response = await Post<VideosResponse>(options, "videos/list", cancellationToken);
            return response;
        }

        /// <summary>
        /// Gets the anonymous videos.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <param name="limit">The limit.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public async Task<VideosResponse> GetAnonymousVideosAsync(int? offset = null, int? limit = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var options = CreatePostData(false);
            options.AddIfNotNull("offset", offset);
            options.AddIfNotNull("limit", limit);

            var response = await Post<VideosResponse>(options, "videos/list", cancellationToken);
            return response;
        }

        #endregion

        #region Video Methods

        /// <summary>
        /// Deletes the video.
        /// </summary>
        /// <param name="videoId">The video identifier.</param>
        /// <param name="deletionToken">The deletion token.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">videoId;Video ID cannot be null or empty</exception>
        public async Task<bool> DeleteVideoAsync(string videoId, string deletionToken = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(videoId))
            {
                throw new ArgumentNullException("videoId", "Video ID cannot be null or empty");
            }

            var postData = CreatePostData(false);
            postData.AddIfNotNull("deleteToken", deletionToken);

            var method = string.Format("video/{0}/delete", videoId);
            var response = await Post<Response>(postData, method, cancellationToken);
            return response != null && response.Status;
        }

        /// <summary>
        /// Gets the video.
        /// </summary>
        /// <param name="videoId">The video identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">videoId;Video ID cannot be null or empty</exception>
        public async Task<Video> GetVideoAsync(string videoId, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(videoId))
            {
                throw new ArgumentNullException("videoId", "Video ID cannot be null or empty");
            }

            var options = CreatePostData(false);
            var method = string.Format("video/{0}", videoId);

            var response = await Get<VideoResponse>(method, options.ToQueryString(), cancellationToken);
            return response != null ? response.Video : null;
        }

        /// <summary>
        /// Edits the video.
        /// </summary>
        /// <param name="videoId">The video identifier.</param>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">videoId;Video ID cannot be null or empty</exception>
        public async Task<Video> EditVideoAsync(string videoId, VideoRequest request = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(videoId))
            {
                throw new ArgumentNullException("videoId", "Video ID cannot be null or empty");
            }

            var postData = CreatePostData(false);
            if (request != null)
            {
                postData.AddIfNotNull("title", request.Title);
                postData.AddIfNotNull("description", request.Description);
                postData.AddIfNotNull("thumbnail", await request.ThumbnailStream.ToBase64String());
                postData.AddIfNotNull("source", request.VideoSource);
                postData.AddIfNotNull("latitude", request.Latitude);
                postData.AddIfNotNull("longitude", request.Longitude);
                postData.AddIfNotNull("place_id", request.FourSquarePlaceId);
                postData.AddIfNotNull("place_name", request.FourSquarePlaceName);
                postData.AddIfNotNull("private", request.IsPrivate);
            }

            var method = string.Format("video/{0}/edit", videoId);

            var response = await Post<VideoResponse>(postData, method, cancellationToken);
            return response != null ? response.Video : null;
        }

        /// <summary>
        /// Flags the video.
        /// </summary>
        /// <param name="videoId">The video identifier.</param>
        /// <param name="isFlagged">if set to <c>true</c> [is flagged].</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">videoId;Video ID cannot be null or empty</exception>
        public async Task<Video> FlagVideoAsync(string videoId, bool isFlagged, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(videoId))
            {
                throw new ArgumentNullException("videoId", "Video ID cannot be null or empty");
            }

            var postData = CreatePostData();
            postData.Add("flagged", isFlagged ? "1" : "0");

            var method = string.Format("video/{0}/flag", videoId);

            var response = await Post<VideoResponse>(postData, method, cancellationToken);
            return response != null ? response.Video : null;
        }

        /// <summary>
        /// Requests the video.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public async Task<VideoRequestResponse> RequestVideoAsync(VideoRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            var postData = CreatePostData(false);
            if (request != null)
            {
                postData.AddIfNotNull("title", request.Title);
                postData.AddIfNotNull("description", request.Description);
                postData.AddIfNotNull("thumbnail", await request.ThumbnailStream.ToBase64String());
                postData.AddIfNotNull("source", request.VideoSource);
                postData.AddIfNotNull("latitude", request.Latitude);
                postData.AddIfNotNull("longitude", request.Longitude);
                postData.AddIfNotNull("place_id", request.FourSquarePlaceId);
                postData.AddIfNotNull("place_name", request.FourSquarePlaceName);
                postData.AddIfNotNull("private", request.IsPrivate);
            }
            
            var response = await Post<VideoRequestResponse>(postData, "video/request", cancellationToken);
            return response;
        }

        /// <summary>
        /// Gets the video thumbnail.
        /// </summary>
        /// <param name="videoId">The video identifier.</param>
        /// <returns></returns>
        public string GetVideoThumbnail(string videoId)
        {
            return CreateUrl(string.Format("video/{0}/thumbnail", videoId));
        }

        /// <summary>
        /// Updates the video title.
        /// </summary>
        /// <param name="videoCode">The video code.</param>
        /// <param name="title">The title.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// videoCode;A video code must be provided
        /// or
        /// title;Title cannot be null or empty
        /// </exception>
        /// <exception cref="System.InvalidOperationException">You must have set a device id</exception>
        public async Task<bool> UpdateVideoTitleAsync(string videoCode, string title, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(videoCode))
            {
                throw new ArgumentNullException("videoCode", "A video code must be provided");
            }

            if (string.IsNullOrEmpty(title))
            {
                throw new ArgumentNullException("title", "Title cannot be null or empty");
            }

            if (string.IsNullOrEmpty(DeviceId))
            {
                throw new InvalidOperationException("You must have set a device id");
            }

            var postData = CreatePostData();
            postData.AddIfNotNull("code", videoCode);
            postData.AddIfNotNull("title", title);

            var response = await Post<Response>(postData, "video/update-title", cancellationToken);
            return response != null && response.Status;
        }

        /// <summary>
        /// Uploads the video.
        /// </summary>
        /// <param name="videoCode">The video code.</param>
        /// <param name="videoStream">The video stream.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// videoCode;A valid video code must be used from the RequestVideo method.
        /// or
        /// videoStream;Invalid video stream passed through
        /// </exception>
        public async Task<VideoUploadResponse> UploadVideoAsync(string videoCode, Stream videoStream, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(videoCode))
            {
                throw new ArgumentNullException("videoCode", "A valid video code must be used from the RequestVideo method.");
            }

            if (videoStream == null || videoStream.Length == 0)
            {
                throw new ArgumentNullException("videoStream", "Invalid video stream passed through");
            }

            var postData = CreatePostData(false);
            postData.AddIfNotNull("code", videoCode);
            postData.AddIfNotNull("filedata", await videoStream.ToBase64String());

            var response = await Post<VideoUploadResponse>(postData, "video/upload", cancellationToken);
            return response;
        }

        /// <summary>
        /// Uploads the video.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="videoStream">The video stream.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">videoStream;Invalid video stream passed through</exception>
        public async Task<VideoUploadResponse> UploadVideoAsync(VideoRequest request, Stream videoStream, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (videoStream == null || videoStream.Length == 0)
            {
                throw new ArgumentNullException("videoStream", "Invalid video stream passed through");
            }

            var postData = CreatePostData(false);
            if (request != null)
            {
                postData.AddIfNotNull("channel", request.ChannelId);
                postData.AddIfNotNull("size", request.VideoSize);
                postData.AddIfNotNull("title", request.Title);
                postData.AddIfNotNull("description", request.Description);
                postData.AddIfNotNull("source", request.VideoSource);
                postData.AddIfNotNull("latitude", request.Latitude);
                postData.AddIfNotNull("longitude", request.Longitude);
                postData.AddIfNotNull("place_id", request.FourSquarePlaceId);
                postData.AddIfNotNull("place_name", request.FourSquarePlaceName);
                postData.AddIfNotNull("private", request.IsPrivate);
                postData.AddIfNotNull("filename", request.FileName);
            }

            var response = await Post<VideoUploadResponse>(postData, "video/upload", cancellationToken);
            return response;
        }

        #endregion

        #region Search Methods

        /// <summary>
        /// Locations the search.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.InvalidOperationException">You must supply a valid request
        /// or
        /// You must supply either long/lat or a geofence ID</exception>
        public async Task<VideosResponse> LocationSearchAsync(LocationRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (request == null)
            {
                throw new InvalidOperationException("You must supply a valid request");
            }

            if (request.Latitude == null && request.Longitude == null && string.IsNullOrEmpty(request.GeofenceId))
            {
                throw new InvalidOperationException("You must supply either long/lat or a geofence ID");
            }

            var postData = CreatePostData(false);
            if (string.IsNullOrEmpty(request.GeofenceId))
            {
                postData.AddIfNotNull("latitude", request.Latitude);
                postData.AddIfNotNull("longitude", request.Longitude);
                postData.AddIfNotNull("from", request.From);
                postData.AddIfNotNull("to", request.To);
                postData.AddIfNotNull("distance", request.Distance);
            }
            else
            {
                postData.AddIfNotNull("geofence", request.GeofenceId);
            }

            postData.AddIfNotNull("offset", request.Offset);
            postData.AddIfNotNull("limit", request.Limit);
            postData.AddIfNotNull("order", request.LocationOrderBy);

            var response = await Post<VideosResponse>(postData, "videos/location", cancellationToken);
            return response;
        }

        /// <summary>
        /// Searches the videos.
        /// </summary>
        /// <param name="searchText">The search text.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">searchText;Search text cannot be null or empty</exception>
        public async Task<VideosResponse> SearchVideosAsync(string searchText, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(searchText))
            {
                throw new ArgumentNullException("searchText", "Search text cannot be null or empty");
            }

            var postData = CreatePostData(false);
            postData.AddIfNotNull("query", searchText);

            var response = await Get<VideosResponse>("videos/search", postData.ToQueryString(), cancellationToken);
            return response;
        }

        #endregion

        #region API Call methods
        private async Task<TReturnType> Post<TReturnType>(Dictionary<string, string> postData, string method, CancellationToken cancellationToken = default(CancellationToken))
        {
            var url = CreateUrl(method);

            var response = await _httpClient.PostAsync(url, new FormUrlEncodedContent(postData), cancellationToken);

            return await HandleResponse<TReturnType>(response);
        }

        private async Task<TReturnType> Get<TReturnType>(string method, string options = "", CancellationToken cancellationToken = default(CancellationToken))
        {
            var url = CreateUrl(method, options);

            var response = await _httpClient.GetAsync(url, cancellationToken);

            return await HandleResponse<TReturnType>(response);
        }

        private static async Task<TReturnType> HandleResponse<TReturnType>(HttpResponseMessage response)
        {
            var responseString = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var returnItem = JsonConvert.DeserializeObject<TReturnType>(responseString);
                return returnItem;
            }

            var error = JsonConvert.DeserializeObject<ErrorResponse>(responseString);
            throw new VidMeException(response.StatusCode, error.Error);
        }
        #endregion

        private void CheckExpirationDateIsOk()
        {
            var now = DateTime.Now;
            if (AuthenticationInfo == null || AuthenticationInfo.Expires < now)
            {
                throw new VidMeException(HttpStatusCode.Unauthorized, "No valid AuthenticationInfo set");
            }
        }

        private static string CreateUrl(string method, string options = "")
        {
            return string.Format("{0}{1}?{2}", BaseUrl, method, options);
        }

        private Dictionary<string, string> CreatePostData(bool tokenRequred = true)
        {
            var postData = new Dictionary<string, string>();
            if (tokenRequred)
            {
                CheckExpirationDateIsOk();
            }

            if(AuthenticationInfo != null)
            {
                postData.Add("token", AuthenticationInfo.Token);
            }
            postData.AddIfNotNull("DEVICE", DeviceId);
            postData.AddIfNotNull("PLATFORM", Platform);

            return postData;
        }
    }
}
