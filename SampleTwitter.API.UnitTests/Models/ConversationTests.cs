using SampleTwitter.API.Exceptions;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.UnitTests.Models;

public class ConversationTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(5, 12, 5, 12)]
    [InlineData(12, 5, 5, 12)]
    public void Start_OrdersParticipantIds_RegardlessOfInitiator(
        long initiatorId,
        long recipientId,
        long expectedA,
        long expectedB)
    {
        // Act
        var conversation = Conversation.Start(initiatorId, recipientId, FixedNow);

        // Assert
        Assert.Equal(expectedA, conversation.ParticipantAId);
        Assert.Equal(expectedB, conversation.ParticipantBId);
        Assert.Equal(FixedNow, conversation.CreatedAt);
        Assert.Null(conversation.LastMessageAt);
        Assert.Null(conversation.ParticipantALastReadAt);
        Assert.Null(conversation.ParticipantBLastReadAt);
    }

    [Fact]
    public void Start_WithSameUser_ThrowsSelfMessageException()
    {
        // Act & Assert
        Assert.Throws<SelfMessageException>(() => Conversation.Start(7, 7, FixedNow));
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(-1, 5)]
    [InlineData(5, 0)]
    [InlineData(5, -2)]
    public void Start_WithNonPositiveUserId_ThrowsArgumentOutOfRangeException(long initiatorId, long recipientId)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => Conversation.Start(initiatorId, recipientId, FixedNow));
    }

    [Fact]
    public void SendMessage_ByParticipant_TrimsTextAndUpdatesLastMessageAt()
    {
        // Arrange
        var conversation = Conversation.Start(1, 2, FixedNow);
        var sentAt = FixedNow.AddMinutes(5);

        // Act
        var message = conversation.SendMessage(1, "  hello there  ", sentAt);

        // Assert
        Assert.Equal(1, message.SenderId);
        Assert.Equal("hello there", message.Text);
        Assert.Equal(sentAt, message.SentAt);
        Assert.Equal(sentAt, conversation.LastMessageAt);
        Assert.Single(conversation.Messages);
    }

    [Fact]
    public void SendMessage_ByNonParticipant_ThrowsForbiddenException()
    {
        // Arrange
        var conversation = Conversation.Start(1, 2, FixedNow);

        // Act & Assert
        Assert.Throws<ForbiddenException>(() => conversation.SendMessage(99, "intruder", FixedNow.AddMinutes(1)));
        Assert.Empty(conversation.Messages);
        Assert.Null(conversation.LastMessageAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \t\n  ")]
    public void SendMessage_BlankText_ThrowsEmptyMessageException(string? text)
    {
        // Arrange
        var conversation = Conversation.Start(1, 2, FixedNow);

        // Act & Assert
        Assert.Throws<EmptyMessageException>(() => conversation.SendMessage(1, text, FixedNow.AddMinutes(1)));
        Assert.Empty(conversation.Messages);
        Assert.Null(conversation.LastMessageAt);
    }

    [Fact]
    public void SendMessage_ExactlyMaxLength_Succeeds()
    {
        // Arrange
        var conversation = Conversation.Start(1, 2, FixedNow);
        var maxText = new string('a', Message.MaxTextLength);

        // Act
        var message = conversation.SendMessage(2, $"  {maxText}  ", FixedNow.AddMinutes(1));

        // Assert
        Assert.Equal(maxText, message.Text);
    }

    [Fact]
    public void SendMessage_TooLong_ThrowsArgumentException()
    {
        // Arrange
        var conversation = Conversation.Start(1, 2, FixedNow);
        var tooLong = new string('x', Message.MaxTextLength + 1);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => conversation.SendMessage(1, tooLong, FixedNow.AddMinutes(1)));
        Assert.Equal("text", ex.ParamName);
        Assert.Empty(conversation.Messages);
    }

    [Fact]
    public void SendMessage_AdvancesSendersOwnReadMarkerOnly()
    {
        // Arrange
        var conversation = Conversation.Start(1, 2, FixedNow);
        var firstSentAt = FixedNow.AddMinutes(5);
        var secondSentAt = FixedNow.AddMinutes(10);

        // Act
        conversation.SendMessage(1, "from A", firstSentAt);

        // Assert
        Assert.Equal(firstSentAt, conversation.ParticipantALastReadAt);
        Assert.Null(conversation.ParticipantBLastReadAt);

        // Act
        conversation.SendMessage(2, "from B", secondSentAt);

        // Assert
        Assert.Equal(firstSentAt, conversation.ParticipantALastReadAt);
        Assert.Equal(secondSentAt, conversation.ParticipantBLastReadAt);
    }

    [Fact]
    public void MarkAsRead_ByParticipant_SetsOnlyThatParticipantsMarker()
    {
        // Arrange
        var conversation = Conversation.Start(1, 2, FixedNow);
        var readAt = FixedNow.AddMinutes(3);

        // Act
        conversation.MarkAsRead(2, readAt);

        // Assert
        Assert.Null(conversation.ParticipantALastReadAt);
        Assert.Equal(readAt, conversation.ParticipantBLastReadAt);
    }

    [Fact]
    public void MarkAsRead_IsIdempotentAndNeverMovesBackwards()
    {
        // Arrange
        var conversation = Conversation.Start(1, 2, FixedNow);
        var laterTime = FixedNow.AddMinutes(10);
        var earlierTime = FixedNow.AddMinutes(5);

        // Act
        conversation.MarkAsRead(1, laterTime);
        conversation.MarkAsRead(1, laterTime);
        conversation.MarkAsRead(1, earlierTime);

        // Assert
        Assert.Equal(laterTime, conversation.ParticipantALastReadAt);
    }

    [Fact]
    public void MarkAsRead_ByNonParticipant_ThrowsForbiddenException()
    {
        // Arrange
        var conversation = Conversation.Start(1, 2, FixedNow);

        // Act & Assert
        Assert.Throws<ForbiddenException>(() => conversation.MarkAsRead(99, FixedNow.AddMinutes(1)));
        Assert.Null(conversation.ParticipantALastReadAt);
        Assert.Null(conversation.ParticipantBLastReadAt);
    }

    [Fact]
    public void GetOtherParticipantId_ReturnsCounterpart()
    {
        // Arrange
        var conversation = Conversation.Start(10, 20, FixedNow);

        // Act & Assert
        Assert.Equal(20, conversation.GetOtherParticipantId(10));
        Assert.Equal(10, conversation.GetOtherParticipantId(20));
    }

    [Fact]
    public void GetOtherParticipantId_ForOutsider_ThrowsForbiddenException()
    {
        // Arrange
        var conversation = Conversation.Start(10, 20, FixedNow);

        // Act & Assert
        Assert.Throws<ForbiddenException>(() => conversation.GetOtherParticipantId(99));
    }
}