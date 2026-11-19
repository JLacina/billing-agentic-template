## Knowledge Transfer: Managing Context in AI-Assisted Development

Based on Chelsea Troy’s talk, *The Curse of Unbounded Contexts: Using Domains as LLM Consumers*.

Effective AI-assisted development requires deliberate management of scope, vocabulary, constraints, and decisions. A continuous chat interface does not guarantee that a growing conversation remains coherent.

### Bounded contexts and shared vocabulary

In domain-driven design (DDD), a **bounded context** defines where a particular model and vocabulary apply. The same word can legitimately mean different things in different contexts:

- **Sales:** a customer is a lead with potential value.
- **Shipping:** a customer is a delivery address and order history.
- **Billing:** a customer is a payment account with invoices and credit status.

Apply the same discipline to AI conversations. Define ambiguous terms before using them, and make translations between contexts explicit.

For example:

> In this conversation, “invoice” means our domain aggregate,
> “customer” means the billing account, and “payment” means recording
> an amount received—not capturing a card transaction.

### Four common failure modes

| Failure mode | Warning sign | Recommended response |
|---|---|---|
| **Vocabulary mismatch** | The answer sounds reasonable but solves a different problem. | Ask what the assistant understood an ambiguous term to mean, then establish a shared definition. |
| **Constraint drift** | You repeatedly say, “Remember, we agreed…” | Restate the current goal and important constraints together. |
| **Mixed goals or working modes** | Responses mix explanation, brainstorming, and implementation. | Declare the intended mode and separate work with different purposes. |
| **Old assumptions surviving a pivot** | Suggestions keep returning to an abandoned approach. | Start a fresh conversation with the new direction and only the conclusions that still apply. |

These are practical diagnostic patterns. Message counts and repeated-correction thresholds are heuristics, not universal limits on model behavior.

### Six practices for maintaining useful context

1. **Start with a conversation charter.**  
   State the scope, define ambiguous vocabulary, and declare the working mode. Include important constraints before requesting substantial work.

   > We are working on invoice payment validation.
   > “Payment” means recording money already received.
   > We are exploring the existing behavior; do not change code yet.

2. **Declare the working mode.**  
   Make the expected output explicit:

   | Mode | What to request |
   |---|---|
   | Exploring | Explain the existing system, assumptions, and risks. |
   | Brainstorming | Generate alternatives for later evaluation. |
   | Deciding | Recommend an approach and explain the tradeoffs. |
   | Implementing | Execute an agreed decision within stated constraints. |

3. **Keep one conversation focused on one domain and purpose.**  
   Separate unrelated tasks. The talk recommends a fresh conversation when moving between substantial phases, such as research and implementation, to reduce carryover from earlier framing.

4. **Transfer conclusions deliberately.**  
   Begin the next conversation with a short handoff containing accepted decisions, relevant constraints, and the next objective. Leave behind abandoned alternatives and unnecessary conversation history.

   > Previous work established:
   > - Payment rules belong in the Invoice aggregate.
   > - Overpayments must return a stable business error.
   > - Persistence changes are outside this task.
   > Next: implement the rule and verify it with tests.

5. **Recognize when repeated correction needs a new boundary.**  
   If the same misunderstanding survives several corrections, reassess the conversation’s framing. A fresh context can prevent old assumptions from shaping the new task. The talk relates this to DDD’s anti-corruption-layer principle: protect one model from assumptions belonging to another.

6. **Periodically re-anchor long conversations.**  
   Restate the current goal, accepted decisions, and constraints. The talk suggests every 15–20 messages as a practical reminder, or earlier when drift appears. Also check whether those constraints still reflect what you want.

### Match the task to the amount of context management it needs

Two questions help determine how much structure a task needs:

1. How clearly is the domain defined?
2. How clearly can success be evaluated?

| Domain clarity | Success criteria | Suggested approach |
|---|---|---|
| Clear | Clear | Use a focused request and verify the result. Examples: function conversion, regex matching, or formatting. |
| Unclear | Clear | Clarify the domain first; an answer can look correct while relying on wrong assumptions. |
| Clear | Unclear | Define evaluation criteria and iterate. Examples: refactoring and code review. |
| Unclear | Unclear | Separate discovery, decisions, and implementation. Architecture work needs especially deliberate context management. |

Small, self-contained tasks usually need less conversational setup. Broader tasks benefit from explicit boundaries and carefully chosen handoffs.

### Applying the lessons in this repository

- Use `CLAUDE.md` for standing architecture rules, commands, and constraints.
- Use `PLAN.md` for task scope, acceptance criteria, and progress.
- Use `docs/adr/` for durable decisions and the rationale future maintainers need.
- Use focused skills for repeatable procedures.
- Start implementation sessions with the relevant decisions and constraints.
- Verify results through tests, automated checks, and human review.

Keep conversation handoffs concise while preserving durable reasoning in project documentation. The objective is to carry forward the context needed for the next task and make its assumptions explicit.
