function onLose(owner, effect, actionContainer, replacing)
    if replacing then return end
    owner:SetProc(2, false);
end;