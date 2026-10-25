namespace SocialContent.Api.Domain;

public enum Format { Mp3, Mp4, Flac, Png, Jpeg, Jpg, Gif, Webp }

public enum FacebookPostType { Text, Photo, Video }

public enum PostStatus { Draft, Scheduled, Publishing, Published, Failed }

public enum SocialPlatform { Facebook, Instagram, TikTok }

// Nazwy wartości zgodne 1:1 z diagramem (wielkie litery).
public enum InstagramMediaType { IMAGE, VIDEO, CAROUSEL, REELS, STORIES }

public enum TikTokPrivacyLevel { PUBLIC_TO_EVERYONE, MUTUAL_FOLLOW_FRIENDS, SELF_ONLY }
