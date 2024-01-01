using System.Security.AccessControl;
using Job.Core.Interfaces.Result;

namespace JobScheduler.Queue.Result;

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