using System;

namespace ReunionMovement.Core
{
    /// <summary>
    /// 引擎初始化结果
    /// </summary>
    public readonly struct InitResult
    {
        /// <summary>是否成功</summary>
        public bool IsSuccess { get; }

        /// <summary>错误消息</summary>
        public string ErrorMessage { get; }

        /// <summary>异常（如有）</summary>
        public Exception Exception { get; }

        private InitResult(bool success, string errorMessage = null, Exception exception = null)
        {
            IsSuccess = success;
            ErrorMessage = errorMessage;
            Exception = exception;
        }

        public static InitResult Success() => new InitResult(true);

        public static InitResult Failure(string errorMessage, Exception exception = null)
            => new InitResult(false, errorMessage, exception);
    }
}
