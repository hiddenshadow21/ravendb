﻿namespace Raven.Server.Documents.Studio
{
    public class FeedbackForm
    {
        public string Message { get; set; }

        public FeedbackProduct Product { get; set; }
        public FeedbackUser User { get; set; }

        public class FeedbackProduct
        {
            public string Name { get; set; }
            public string Version { get; set; }
            public string StudioVersion { get; set; }
        }

        public class FeedbackUser
        {
            public string Name { get; set; }
            public string Email { get; set; }
            public string UserAgent { get; set; }
        }
    }
}