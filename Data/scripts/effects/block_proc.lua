function onLose(owner, effect, actionContainer, replacing)
    if replacing then return end
    owner:SetProc(1, false);
end;