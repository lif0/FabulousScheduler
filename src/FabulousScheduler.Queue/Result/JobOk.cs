using System.Security.AccessControl;
using FabulousScheduler.Core.Interfaces.Result;

namespace FabulousScheduler.Queue.Result;

public class JobOk : IJobOk
{
	public JobOk(Guid id, string name)
	{
		Id = id;
		Name = name;
	}

	public Guid Id { get; }
	public string Name { get; }
}