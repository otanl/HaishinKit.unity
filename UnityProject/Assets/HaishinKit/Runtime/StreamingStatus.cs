namespace HaishinKit
{
    /// <summary>
    /// 配信の状態を表す列挙型
    /// </summary>
    public enum StreamingStatus
    {
        /// <summary>切断状態</summary>
        Disconnected,

        /// <summary>接続中</summary>
        Connecting,

        /// <summary>接続済み（未配信）</summary>
        Connected,

        /// <summary>配信中</summary>
        Publishing,

        /// <summary>配信停止処理中</summary>
        Stopping,

        /// <summary>エラー発生</summary>
        Error
    }
}
