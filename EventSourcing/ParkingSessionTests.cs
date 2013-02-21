using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;

namespace EventSourcing
{
    [TestFixture]
    public class ParkingSessionTests
    {
        [Test]
        public void GivenAParkingSession_ExtensionShouldWork()
        {
            //given:

            var location = new ParkingSession("452");
         
            location.Hydrate(new[] { new ParkingSessionStarted(
                        userId: "123", 
                        startTime: new DateTime(2013, 1, 1, 16, 0, 0))
                });

            //when:
            var events = location.Consume(
                new ExtendParkingSession(byMinutes : 30)).ToList();

            //then:
            Debug.Assert(events != null, "events != null");
            Assert.That((events[0] as ParkingSessionExtended).ByMinutes, Is.EqualTo(30));
        }

        [Test, ExpectedException(typeof(InvalidExtensionDurationException))]
        public void GivenAParkingSession_NegativeExtensionShouldNotWork()
        {
            //given:

            var location = new ParkingSession("452");
         
            location.Hydrate(new[] { new ParkingSessionStarted(
                        userId: "123", 
                        startTime: new DateTime(2013, 1, 1, 16, 0, 0))
                });

            //when:
            var events = location.Consume(
                new ExtendParkingSession(byMinutes : -5)).ToList();

            //then:
            Debug.Assert(events != null, "events != null");
            Assert.That((events[0] as ParkingSessionExtended).ByMinutes, Is.EqualTo(-5));
        }

        [Test]
        public void GivenASecondParkingSessionExtensionShouldWork()
        {
            //given:

            var location = new ParkingSession("452");
            location.Hydrate(new Event[]
            {
                new ParkingSessionStarted(
                                 userId : "123", 
                                 startTime : new DateTime(2013, 1, 1, 16, 0, 0)),
                new ParkingSessionExtended(30)
            });

            //when:
            var events = location.Consume(
                new ExtendParkingSession(byMinutes : 30)).ToList();

            //then:
            Debug.Assert(events != null, "events != null");
            Assert.That(events.ToList()[0] is ParkingSessionExtended);
        }

        [Test]
        public void GivenTwoParkingSessionExtensionCommands_ShouldExtendByTotalDuration()
        {
            //given:

            var location = new ParkingSession("452");
            location.Hydrate(new Event[]
            {
                new ParkingSessionStarted(
                                 userId : "123", 
                                 startTime : new DateTime(2013, 1, 1, 16, 0, 0))
            });

            //when:
            var events = location.Consume(
                new ExtendParkingSession(byMinutes : 30)).ToList();

            events.AddRange(location.Consume(
                new ExtendParkingSession(byMinutes: 30)));

            //then:
            Debug.Assert(events != null, "events != null");
            Assert.That(events.ToList()[0] is ParkingSessionExtended);
            Assert.That(events.ToList()[1] is ParkingSessionExtended);

            
            Assert.That(events.Cast<ParkingSessionExtended>().ToList().Sum(x => x.ByMinutes) == 60);

        }

        [Test, ExpectedException(typeof(MaximumStayExceededException))]
        public void GivenTwoParkingSessionExtensionCommandsTotallingMoreThanMaximumStay_SecondCommandRejectedDueToMaximumStayExceeded()
        {
            //given:

            var location = new ParkingSession("452", 30);
            location.Hydrate(new Event[]
            {
                new ParkingSessionStarted(
                                 userId : "123", 
                                 startTime : new DateTime(2013, 1, 1, 16, 0, 0))
            });

            //when:
            var events = location.Consume(
                new ExtendParkingSession(byMinutes : 30)).ToList();

            events.AddRange(location.Consume(
                new ExtendParkingSession(byMinutes: 30)));

            //then:
            Assert.Fail("we should not get here");
        }

        [Test, ExpectedException(typeof(MaximumStayExceededException))]
        public void GivenOneParkingSessionExtensionCommandAndOneParkingSessionExtendedEventTotallingMoreThanMaximumStay_CommandRejectedDueToMaximumStayExceeded()
        {
            //given:

            var location = new ParkingSession("452", 30);
            location.Hydrate(new Event[]
            {
                new ParkingSessionStarted(
                                 userId : "123", 
                                 startTime : new DateTime(2013, 1, 1, 16, 0, 0)), new ParkingSessionExtended(30), 
            });

            //when:
            var events = location.Consume(
                new ExtendParkingSession(byMinutes: 30)).ToList();

            //then:
            Assert.Fail("we should not get here");
        }

    }

    public class MaximumStayExceededException : Exception
    {
    }

    public class InvalidExtensionDurationException : Exception
    { }

    public class ParkingSessionExtended : Event
    {
        public int ByMinutes { get; private set; }

        public ParkingSessionExtended(int byMinutes)
        {
            ByMinutes = byMinutes;
        }

        public ParkingSessionExtended() { }
    }

    public class ParkingSessionStarted : Event
    {
        public string UserId { get; private set; }
        public DateTime StartTime { get; private set; }

        public ParkingSessionStarted(string userId, DateTime startTime)
        {
            UserId = userId;
            StartTime = startTime;
        }
    }

    public class ExtendParkingSession : Command
    {
        public int ByMinutes { get; internal set; }

        public ExtendParkingSession(int byMinutes)
        {
            ByMinutes = byMinutes;
        }
    }

    public interface Command
    {
    }

    public class ParkingSession
    {
        private readonly string _locationId;
        private readonly TimeSpan _maximumStay;
        private DateTime _startTime;
        private string _userId;
        private TimeSpan _duration;

        public ParkingSession(string locationId)
        {
            _locationId = locationId;
            _maximumStay = TimeSpan.FromDays(1);
            _duration = TimeSpan.FromTicks(0);
        }

        public ParkingSession(string locationId, int maximumStayInMinutes)
        {
            _locationId = locationId;
            _maximumStay = TimeSpan.FromMinutes(maximumStayInMinutes);
            _duration = TimeSpan.FromTicks(0);
        }

        public void Hydrate(IEnumerable<Event> events)
        {
            foreach (var domainEvent in events)
            {
                Handle(domainEvent);
            };
        }

        private void Handle(Event domainEvent)
        {
            if (domainEvent is ParkingSessionStarted) Handle(domainEvent as ParkingSessionStarted);
            else if (domainEvent is ParkingSessionExtended) Handle(domainEvent as ParkingSessionExtended);
            else throw new NotImplementedException("new event not handled");
        }
        private void Handle(ParkingSessionStarted parkingSessionStarted)
        {
            _startTime = parkingSessionStarted.StartTime;
            _userId = parkingSessionStarted.UserId;
        }
        private void Handle(ParkingSessionExtended parkingSessionExtended)
        {
            _duration = _duration.Add(TimeSpan.FromMinutes(parkingSessionExtended.ByMinutes));
        }

        public IEnumerable<Event> Consume(Command command)
        {
            ExtendParkingSession parkingCommand = (ExtendParkingSession) command;
            if (parkingCommand is ExtendParkingSession
                && ((ExtendParkingSession)command).ByMinutes <= 0)
                throw new InvalidExtensionDurationException();
            if ((parkingCommand.ByMinutes + _duration.TotalMinutes) > _maximumStay.TotalMinutes)
                throw new MaximumStayExceededException();

            var parkingSessionExtended = new ParkingSessionExtended(
                ((ExtendParkingSession)command).ByMinutes);
            Handle(parkingSessionExtended);
            yield return parkingSessionExtended;
        }
    }

    public interface Event
    {
    }
}
