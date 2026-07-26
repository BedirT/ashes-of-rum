[.[][] |
  select(.body != null) |
  . as $item |
  try {
    head: ($item.body | capture("(?m)^reviewed-head:[[:space:]]*`?(?<value>[0-9a-f]{40})`?[[:space:]]*$").value),
    round: ($item.body | capture("(?m)^review-round:[[:space:]]*(?<value>[1-9][0-9]*)[[:space:]]*$").value | tonumber),
    blockers: ($item.body | capture("(?m)^blocking-findings:[[:space:]]*(?<value>[0-9]+)[[:space:]]*$").value | tonumber),
    submittedAt: ($item.submitted_at // $item.created_at)
  } catch empty] |
sort_by(.submittedAt) |
last // empty
