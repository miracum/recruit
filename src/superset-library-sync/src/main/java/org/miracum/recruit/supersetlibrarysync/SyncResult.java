package org.miracum.recruit.supersetlibrarysync;

/**
 * The outcome of one sync run: {@code savedQueriesConsidered} is every candidate saved query found
 * (after tag filtering), which may be more than {@code synced + failed} - saved queries with no
 * recognized SQL annotation are silently skipped, not counted as failures.
 */
public record SyncResult(int savedQueriesConsidered, int synced, int failed) {

  public boolean hasFailures() {
    return failed > 0;
  }
}
